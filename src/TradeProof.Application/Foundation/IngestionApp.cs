using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed partial class TradeProofApp
{
    private const int MaxRawUploadBytes = 20 * 1024 * 1024;
    private const int MaxCsvDataRows = 100_000;
    private const string ImportCatalogVersion = "instrument_catalog_local_v1";

    private static readonly string[] BinanceSpotTradeHistoryHeader =
    [
        "Date(UTC)",
        "Pair",
        "Side",
        "Price",
        "Executed",
        "Amount",
        "Fee"
    ];

    private static readonly string[] ImportRowDispositions =
    [
        "RECONCILED",
        "DUPLICATE",
        "ACCOUNTING_PENDING",
        "QUARANTINED"
    ];

    private readonly Dictionary<string, ObjectIngestReservationRecord> _objectIngestReservations = [];
    private readonly Dictionary<string, List<ObjectIngestReservationEventRecord>> _objectIngestReservationEvents = [];
    private readonly Dictionary<string, ProviderObjectVersion> _providerObjectsByUploadId = [];
    private readonly Dictionary<string, UploadRecord> _uploads = [];
    private readonly Dictionary<string, List<UploadStateEventRecord>> _uploadEvents = [];
    private readonly Dictionary<string, UploadObjectLeaseRecord> _uploadObjectLeases = [];
    private readonly Dictionary<string, UploadObjectAbsenceVerificationRecord> _uploadAbsenceVerifications = [];
    private readonly Dictionary<string, ImportPreviewRecord> _importPreviews = [];
    private readonly Dictionary<string, ImportBatchRecord> _importBatches = [];
    private readonly Dictionary<string, StagedFillRecord> _stagedFills = [];
    private readonly Dictionary<string, StagedFillDispositionRecord> _stagedFillDispositions = [];

    public Task<CommandResult<ObjectIngestReservationRecord>> ReserveRawUploadAsync(
        ActorContext actor,
        ReserveRawUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ReserveRawUpload", request.IdempotencyKey, request, () =>
                {
                    ValidateTradingAccount(actor, request.TradingAccountId);
                    if (request.UploadKind != "CSV")
                    {
                        throw new TradeProofException("UPLOAD_KIND_UNSUPPORTED");
                    }

                    if (request.AdapterContractVersion != ContractVersions.BinanceSpotTradeHistoryCsv)
                    {
                        throw new TradeProofException("IMPORT_ADAPTER_UNSUPPORTED");
                    }

                    DateTimeOffset now = Now;
                    string reservationId = NextId("oir");
                    string uploadId = NextId("upl");
                    int leaseGeneration = 1;
                    string providerKeySha256 = ContractVersions.Sha256Utf8($"{actor.WorkspaceId}\u001F{reservationId}\u001F{uploadId}\u001F{leaseGeneration}");
                    string writeCapability = BuildWriteCapability(actor.WorkspaceId, reservationId, providerKeySha256, leaseGeneration, now.AddMinutes(15));

                    TenantControlJobRecord finalizer = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ObjectIngestFinalize,
                        "ObjectIngestReservation",
                        ObjectIngestSubject(reservationId),
                        JsonSerializer.Serialize(new
                        {
                            leaseGeneration,
                            purpose = "RAW_UPLOAD"
                        }, ContractVersions.JsonOptions),
                        $"object-ingest-reservation:{reservationId}:finalize");

                    ObjectIngestReservationRecord reservation = new(
                        reservationId,
                        actor.WorkspaceId,
                        request.TradingAccountId,
                        "RAW_UPLOAD",
                        uploadId,
                        null,
                        "CSV",
                        request.AdapterContractVersion,
                        leaseGeneration,
                        providerKeySha256,
                        writeCapability,
                        "RESERVED",
                        now,
                        now.AddMinutes(15),
                        now.AddHours(1),
                        null,
                        null,
                        finalizer.TenantControlJobId);
                    _objectIngestReservations[reservationId] = reservation;
                    _objectIngestReservationEvents[reservationId] =
                    [
                        new ObjectIngestReservationEventRecord(NextId("oirevt"), reservationId, actor.WorkspaceId, 1, "RESERVE", now, null)
                    ];
                    return reservation;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ObjectIngestReservationRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ProviderWriteResult>> RecordReservedBytesAsync(
        ActorContext actor,
        RecordReservedBytesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                EnsureIdempotencyKey(request.IdempotencyKey);
                ObjectIngestReservationRecord reservation = GetOwnedReservation(actor, request.ObjectIngestReservationId);
                if (reservation.State != "RESERVED" || reservation.WriteCapabilityConsumedAt is not null)
                {
                    throw new TradeProofException("WRITE_CAPABILITY_ALREADY_CONSUMED");
                }

                if (!string.Equals(reservation.WriteCapabilityId, request.WriteCapabilityId, StringComparison.Ordinal))
                {
                    throw new TradeProofException("WRITE_CAPABILITY_INVALID");
                }

                if (Now >= reservation.WriteExpiresAt)
                {
                    throw new TradeProofException("WRITE_CAPABILITY_EXPIRED");
                }

                if (_providerObjectsByUploadId.ContainsKey(reservation.ReservedUploadId))
                {
                    throw new TradeProofException("PROVIDER_OBJECT_ALREADY_EXISTS");
                }

                DateTimeOffset now = Now;
                string contentSha256 = ContractVersions.Sha256Hex(request.Bytes);
                string providerObjectVersionId = $"pov_{contentSha256[..24]}";
                _providerObjectsByUploadId[reservation.ReservedUploadId] = new ProviderObjectVersion(
                    reservation.ObjectIngestReservationId,
                    reservation.ReservedUploadId,
                    providerObjectVersionId,
                    contentSha256,
                    request.Bytes.LongLength,
                    request.Bytes.ToArray(),
                    now);

                ObjectIngestReservationRecord recorded = reservation with
                {
                    State = "BYTES_RECORDED",
                    WriteCapabilityConsumedAt = now
                };
                _objectIngestReservations[reservation.ObjectIngestReservationId] = recorded;
                AddReservationEvent(recorded, "RECORD_BYTES", null, now);
                return Task.FromResult(CommandResult<ProviderWriteResult>.Ok(new ProviderWriteResult(
                    reservation.ObjectIngestReservationId,
                    providerObjectVersionId,
                    contentSha256,
                    request.Bytes.LongLength,
                    now)));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ProviderWriteResult>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<UploadTransferResponse>> TransferRawUploadAsync(
        ActorContext actor,
        TransferRawUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "TransferRawUpload", request.IdempotencyKey, request, () =>
                {
                    ObjectIngestReservationRecord reservation = GetOwnedReservation(actor, request.ObjectIngestReservationId);
                    if (reservation.State == "TRANSFERRED")
                    {
                        return BuildTransferResponse(reservation.ReservedUploadId);
                    }

                    if (reservation.State != "BYTES_RECORDED")
                    {
                        throw new TradeProofException("RESERVED_BYTES_NOT_FOUND");
                    }

                    if (!_providerObjectsByUploadId.TryGetValue(reservation.ReservedUploadId, out ProviderObjectVersion? providerObject))
                    {
                        throw new TradeProofException("PROVIDER_OBJECT_NOT_FOUND");
                    }

                    DateTimeOffset now = Now;
                    TenantControlJobRecord validateJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.UploadValidate,
                        "Upload",
                        UploadSubject(reservation.ReservedUploadId),
                        JsonSerializer.Serialize(new
                        {
                            adapterContractVersion = reservation.AdapterContractVersion,
                            leaseGeneration = reservation.LeaseGeneration,
                            tradingAccountRecordKey = new { trading_account_id = reservation.TradingAccountId },
                            uploadKind = reservation.ExpectedUploadKind
                        }, ContractVersions.JsonOptions),
                        $"upload:{reservation.ReservedUploadId}:validate");

                    TenantControlJobRecord purgeJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.UploadPurge,
                        "Upload",
                        UploadSubject(reservation.ReservedUploadId),
                        JsonSerializer.Serialize(new
                        {
                            leaseGeneration = reservation.LeaseGeneration
                        }, ContractVersions.JsonOptions),
                        $"upload:{reservation.ReservedUploadId}:purge");

                    UploadRecord upload = new(
                        reservation.ReservedUploadId,
                        actor.WorkspaceId,
                        reservation.TradingAccountId,
                        reservation.ExpectedUploadKind,
                        reservation.AdapterContractVersion,
                        "QUARANTINED",
                        providerObject.ContentSha256,
                        providerObject.SizeBytes,
                        now,
                        now.AddHours(20),
                        now.AddHours(24),
                        reservation.ObjectIngestReservationId,
                        reservation.LeaseGeneration,
                        validateJob.TenantControlJobId,
                        purgeJob.TenantControlJobId,
                        null,
                        null,
                        null);
                    _uploads[upload.UploadId] = upload;
                    _uploadEvents[upload.UploadId] =
                    [
                        new UploadStateEventRecord(NextId("uplevt"), actor.WorkspaceId, upload.UploadId, 1, "RECEIVE", now, "USER", null, null)
                    ];
                    UploadObjectLeaseRecord lease = new(
                        NextId("upllease"),
                        actor.WorkspaceId,
                        upload.UploadId,
                        reservation.LeaseGeneration,
                        providerObject.ProviderObjectVersionId,
                        "ACTIVE",
                        now,
                        null);
                    _uploadObjectLeases[upload.UploadId] = lease;

                    ObjectIngestReservationRecord transferred = reservation with
                    {
                        State = "TRANSFERRED",
                        TransferredAt = now
                    };
                    _objectIngestReservations[reservation.ObjectIngestReservationId] = transferred;
                    AddReservationEvent(transferred, "TRANSFER", null, now);
                    return new UploadTransferResponse(upload, validateJob, purgeJob, lease);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<UploadTransferResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<UploadValidationResponse>> ValidateUploadAsync(
        ActorContext actor,
        ValidateUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                EnsureIdempotencyKey(request.IdempotencyKey);
                UploadRecord upload = GetOwnedUpload(actor, request.UploadId);
                ImportPreviewRecord? existingPreview = _importPreviews.Values.SingleOrDefault(p => p.UploadId == upload.UploadId);
                if (upload.State is "ACCEPTED" or "REJECTED")
                {
                    return Task.FromResult(CommandResult<UploadValidationResponse>.Ok(new UploadValidationResponse(upload, existingPreview, upload.SafeErrorCode)));
                }

                if (upload.State != "QUARANTINED" && upload.State != "VALIDATING")
                {
                    throw new TradeProofException("UPLOAD_NOT_VALIDATABLE");
                }

                UploadRecord validating = upload.State == "VALIDATING" ? upload : upload with { State = "VALIDATING" };
                _uploads[upload.UploadId] = validating;
                if (upload.State != "VALIDATING")
                {
                    AddUploadEvent(validating, "START_VALIDATION", "SYSTEM", null, null, Now);
                }

                if (!_providerObjectsByUploadId.TryGetValue(upload.UploadId, out ProviderObjectVersion? providerObject))
                {
                    return Task.FromResult(CommandResult<UploadValidationResponse>.Ok(RejectUpload(validating, "PROVIDER_OBJECT_NOT_FOUND")));
                }

                CsvPreviewData previewData;
                try
                {
                    previewData = ParseBinanceCsv(providerObject.Bytes, providerObject.ContentSha256, providerObject.SizeBytes);
                }
                catch (TradeProofException ex)
                {
                    return Task.FromResult(CommandResult<UploadValidationResponse>.Ok(RejectUpload(validating, ex.Code)));
                }

                DateTimeOffset now = Now;
                string previewId = NextId("impprev");
                object summary = new
                {
                    adapterContractVersion = upload.AdapterContractVersion,
                    dataRows = previewData.DataRows,
                    fileSha256 = upload.FileSha256,
                    fileSizeBytes = upload.FileSizeBytes,
                    firstTradeAt = previewData.FirstTradeAt,
                    lastTradeAt = previewData.LastTradeAt,
                    schemaVersion = ContractVersions.ImportPreview,
                    symbols = previewData.Symbols,
                    uploadRecordKey = new { upload_id = upload.UploadId }
                };
                string summarySha256 = ContractVersions.Sha256CanonicalJson(summary);
                ImportPreviewRecord preview = new(
                    previewId,
                    actor.WorkspaceId,
                    upload.UploadId,
                    upload.TradingAccountId,
                    ContractVersions.ImportPreview,
                    upload.AdapterContractVersion,
                    "READY",
                    previewData.DataRows,
                    previewData.Symbols,
                    previewData.FirstTradeAt,
                    previewData.LastTradeAt,
                    summarySha256,
                    now,
                    now.AddMinutes(30),
                    null,
                    null,
                    []);
                _importPreviews[previewId] = preview;

                UploadRecord accepted = upload with
                {
                    State = "ACCEPTED",
                    AcceptedAt = now,
                    SafeErrorCode = null
                };
                _uploads[upload.UploadId] = accepted;
                AddUploadEvent(accepted, "ACCEPT", "SYSTEM", null, null, now);
                TerminalizeTenantWorkCore(upload.ValidateTenantControlJobId, "UPLOAD_ACCEPTED");
                RecordAnalytics(actor.WorkspaceId, "import_previewed", new { import_preview_id = preview.ImportPreviewId }, new { data_rows = preview.DataRows }, now);
                return Task.FromResult(CommandResult<UploadValidationResponse>.Ok(new UploadValidationResponse(accepted, preview, null)));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<UploadValidationResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ImportBatchRecord>> ConfirmImportAsync(
        ActorContext actor,
        ConfirmImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ConfirmImport", request.IdempotencyKey, request, () =>
                {
                    if (!_importPreviews.TryGetValue(request.ImportPreviewId, out ImportPreviewRecord? preview) || preview.WorkspaceId != actor.WorkspaceId)
                    {
                        throw new TradeProofException("IMPORT_PREVIEW_NOT_FOUND");
                    }

                    if (!string.Equals(preview.PreviewSummarySha256, request.PreviewSummarySha256, StringComparison.Ordinal))
                    {
                        throw new TradeProofException("IMPORT_PREVIEW_HASH_MISMATCH");
                    }

                    if (preview.ConfirmedImportBatchId is not null)
                    {
                        return _importBatches[preview.ConfirmedImportBatchId];
                    }

                    if (preview.State != "READY")
                    {
                        throw new TradeProofException("IMPORT_PREVIEW_NOT_READY");
                    }

                    if (Now >= preview.ExpiresAt)
                    {
                        throw new TradeProofException("IMPORT_PREVIEW_EXPIRED");
                    }

                    DateTimeOffset now = Now;
                    string batchId = NextId("impbatch");
                    TenantControlJobRecord importJob = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.Import,
                        "ImportBatch",
                        ImportBatchSubject(batchId),
                        JsonSerializer.Serialize(new
                        {
                            adapterContractVersion = preview.AdapterContractVersion,
                            importPreviewRecordKey = new { import_preview_id = preview.ImportPreviewId },
                            previewSummarySha256 = preview.PreviewSummarySha256,
                            sourceUploadRecordKey = new { upload_id = preview.UploadId }
                        }, ContractVersions.JsonOptions),
                        $"import-batch:{batchId}:import");

                    ImportBatchRecord batch = new(
                        batchId,
                        actor.WorkspaceId,
                        preview.TradingAccountId,
                        preview.UploadId,
                        preview.ImportPreviewId,
                        preview.SchemaVersion,
                        preview.PreviewSummarySha256,
                        preview.AdapterContractVersion,
                        now,
                        "UPLOADED",
                        preview.DataRows,
                        0,
                        0,
                        0,
                        0,
                        null,
                        importJob.TenantControlJobId);
                    _importBatches[batchId] = batch;
                    _importPreviews[preview.ImportPreviewId] = preview with
                    {
                        State = "CONFIRMED",
                        ConfirmedAt = now,
                        ConfirmedImportBatchId = batchId
                    };
                    return batch;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ImportBatchRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<UploadPurgeResponse>> PurgeUploadAsync(
        ActorContext actor,
        PurgeUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "PurgeUpload", request.IdempotencyKey, request, () =>
                {
                    UploadRecord upload = GetOwnedUpload(actor, request.UploadId);
                    if (_uploadAbsenceVerifications.TryGetValue(upload.UploadId, out UploadObjectAbsenceVerificationRecord? existingAbsence))
                    {
                        return new UploadPurgeResponse(upload, existingAbsence);
                    }

                    if (upload.State is "QUARANTINED" or "VALIDATING" && Now >= upload.ForcedPurgeAt)
                    {
                        upload = RejectUpload(upload, "RAW_UPLOAD_RETENTION_DEADLINE").Upload;
                    }

                    if (!_providerObjectsByUploadId.TryGetValue(upload.UploadId, out ProviderObjectVersion? providerObject))
                    {
                        throw new TradeProofException("PROVIDER_OBJECT_NOT_FOUND");
                    }

                    DateTimeOffset now = Now;
                    _providerObjectsByUploadId.Remove(upload.UploadId);
                    UploadObjectAbsenceVerificationRecord absence = new(
                        NextId("abs"),
                        actor.WorkspaceId,
                        upload.UploadId,
                        upload.LeaseGeneration,
                        providerObject.ContentSha256,
                        now);
                    _uploadAbsenceVerifications[upload.UploadId] = absence;

                    UploadObjectLeaseRecord lease = _uploadObjectLeases[upload.UploadId];
                    _uploadObjectLeases[upload.UploadId] = lease with
                    {
                        State = "ABSENCE_VERIFIED",
                        TerminalAt = now
                    };

                    UploadRecord purged = upload with
                    {
                        State = "PURGED",
                        PurgedAt = now
                    };
                    _uploads[upload.UploadId] = purged;
                    AddUploadEvent(purged, "PURGE", "SYSTEM", null, absence.UploadObjectAbsenceVerificationId, now);
                    TerminalizeTenantWorkCore(upload.PurgeTenantControlJobId, "UPLOAD_ABSENCE_VERIFIED");
                    return new UploadPurgeResponse(purged, absence);
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<UploadPurgeResponse>.Fail(ex.Code));
            }
        }
    }

    public Task<int> FinalizeObjectIngestReservationsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            int count = 0;
            foreach (ObjectIngestReservationRecord reservation in _objectIngestReservations.Values.Where(r => r.WorkspaceId == actor.WorkspaceId).ToList())
            {
                if (_markersByJob.ContainsKey(reservation.FinalizeTenantControlJobId))
                {
                    continue;
                }

                if (reservation.State == "TRANSFERRED")
                {
                    TerminalizeTenantWorkCore(reservation.FinalizeTenantControlJobId, "INGEST_ACTIVATED_CLEAN");
                    count++;
                    continue;
                }

                if (Now >= reservation.WriteExpiresAt)
                {
                    if (_providerObjectsByUploadId.Remove(reservation.ReservedUploadId))
                    {
                        AddReservationEvent(reservation, "ABORT_DELETE", "WRITE_EXPIRED", Now);
                    }

                    ObjectIngestReservationRecord aborted = reservation with { State = "ABORT_VERIFIED" };
                    _objectIngestReservations[reservation.ObjectIngestReservationId] = aborted;
                    AddReservationEvent(aborted, "ABORT_VERIFY", "WRITE_EXPIRED", Now);
                    TerminalizeTenantWorkCore(reservation.FinalizeTenantControlJobId, "INGEST_ABORT_ABSENCE_VERIFIED");
                    count++;
                }
            }

            return Task.FromResult(count);
        }
    }

    public Task<CommandResult<ImportProgressResponse>> GetImportProgressAsync(
        ActorContext actor,
        string importBatchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                if (!_importBatches.TryGetValue(importBatchId, out ImportBatchRecord? batch) || batch.WorkspaceId != actor.WorkspaceId)
                {
                    throw new TradeProofException("IMPORT_BATCH_NOT_FOUND");
                }

                ImportPreviewRecord preview = _importPreviews[batch.SourceImportPreviewId];
                return Task.FromResult(CommandResult<ImportProgressResponse>.Ok(new ImportProgressResponse(
                    batch.ImportBatchId,
                    batch.SourceUploadId,
                    batch.SourceImportPreviewId,
                    batch.Status,
                    batch.DataRows,
                    batch.ReconciledRows,
                    batch.DuplicateRows,
                    batch.AccountingPendingRows,
                    batch.QuarantinedRows,
                    preview.SafeErrors,
                    ImportRowDispositions,
                    BuildImportEpisodeSummaries(batch.ImportBatchId))));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ImportProgressResponse>.Fail(ex.Code));
            }
        }
    }

    public StagedFillRecord CreateStagedFillCandidateForTest(ActorContext actor, CreateStagedFillCandidateRequest request)
    {
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            if (!_importBatches.TryGetValue(request.ImportBatchId, out ImportBatchRecord? batch) || batch.WorkspaceId != actor.WorkspaceId)
            {
                throw new TradeProofException("IMPORT_BATCH_NOT_FOUND");
            }

            DateTimeOffset executedAt = ParseUtc(request.ExecutedAt);
            string symbol = NormalizeSymbol(request.VenueSymbol);
            string side = NormalizeSide(request.Side);
            string price = ContractVersions.CanonicalizeDecimal(request.PriceQuotePerBase, 20, 18, true);
            string quantity = ContractVersions.CanonicalizeDecimal(request.ExecutedQtyBase, 20, 18, true);
            string gross = ContractVersions.CanonicalizeDecimal(request.GrossAmountQuote, 20, 18, true);
            string fee = ContractVersions.CanonicalizeDecimal(request.FeeQty, 20, 18, false);
            string feeAsset = NormalizeAsset(request.FeeAsset);
            object fingerprintInput = new
            {
                adapterContractVersion = batch.AdapterContractVersion,
                executedAt,
                fee,
                feeAsset,
                gross,
                price,
                quantity,
                side,
                sourceRowNumber = request.SourceRowNumber,
                symbol
            };
            string fingerprint = ContractVersions.Sha256CanonicalJson(fingerprintInput);
            string signature = ContractVersions.Sha256CanonicalJson(new
            {
                executedAt,
                fee,
                feeAsset,
                gross,
                price,
                quantity,
                side,
                symbol,
                venue = "BINANCE"
            });
            StagedFillRecord staged = new(
                NextId("stg"),
                actor.WorkspaceId,
                batch.TradingAccountId,
                batch.ImportBatchId,
                request.SourceRowNumber,
                ContractVersions.StagedFill,
                ImportCatalogVersion,
                "BINANCE",
                "SPOT",
                symbol,
                side,
                executedAt,
                price,
                quantity,
                gross,
                fee,
                feeAsset,
                fingerprint,
                signature,
                Now);
            _stagedFills[staged.StagedFillId] = staged;
            return staged;
        }
    }

    public StagedFillDispositionRecord ResolveStagedFillForTest(ActorContext actor, string stagedFillId, string outcome, string targetFillId)
    {
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            if (!_stagedFills.TryGetValue(stagedFillId, out StagedFillRecord? staged) || staged.WorkspaceId != actor.WorkspaceId)
            {
                throw new TradeProofException("STAGED_FILL_NOT_FOUND");
            }

            if (outcome is not ("ADMITTED_AS_NEW" or "DISCARDED_AS_DUPLICATE"))
            {
                throw new TradeProofException("STAGED_FILL_DISPOSITION_INVALID");
            }

            if (_stagedFillDispositions.TryGetValue(stagedFillId, out StagedFillDispositionRecord? existing))
            {
                if (existing.Outcome == outcome &&
                    (outcome == "ADMITTED_AS_NEW" ? existing.NormalizedFillId : existing.DuplicateOfFillId) == targetFillId)
                {
                    return existing;
                }

                throw new TradeProofException("STAGED_FILL_DISPOSITION_CONFLICT");
            }

            StagedFillDispositionRecord disposition = new(
                NextId("stgdisp"),
                actor.WorkspaceId,
                staged.StagedFillId,
                outcome,
                outcome == "ADMITTED_AS_NEW" ? targetFillId : null,
                outcome == "DISCARDED_AS_DUPLICATE" ? targetFillId : null,
                Now);
            _stagedFillDispositions[stagedFillId] = disposition;
            return disposition;
        }
    }

    public IReadOnlyList<ObjectIngestReservationRecord> ObjectIngestReservations
    {
        get
        {
            lock (_gate)
            {
                return _objectIngestReservations.Values.OrderBy(r => r.CreatedAt).ToList();
            }
        }
    }

    public IReadOnlyList<UploadRecord> Uploads
    {
        get
        {
            lock (_gate)
            {
                return _uploads.Values.OrderBy(u => u.CreatedAt).ToList();
            }
        }
    }

    public IReadOnlyList<UploadObjectLeaseRecord> UploadObjectLeases
    {
        get
        {
            lock (_gate)
            {
                return _uploadObjectLeases.Values.OrderBy(l => l.CreatedAt).ToList();
            }
        }
    }

    public IReadOnlyList<UploadObjectAbsenceVerificationRecord> UploadAbsenceVerifications
    {
        get
        {
            lock (_gate)
            {
                return _uploadAbsenceVerifications.Values.OrderBy(v => v.VerifiedAbsentAt).ToList();
            }
        }
    }

    public IReadOnlyList<ImportPreviewRecord> ImportPreviews
    {
        get
        {
            lock (_gate)
            {
                return _importPreviews.Values.OrderBy(p => p.CreatedAt).ToList();
            }
        }
    }

    public IReadOnlyList<ImportBatchRecord> ImportBatches
    {
        get
        {
            lock (_gate)
            {
                return _importBatches.Values.OrderBy(b => b.ConfirmedAt).ToList();
            }
        }
    }

    public IReadOnlyList<StagedFillRecord> StagedFills
    {
        get
        {
            lock (_gate)
            {
                return _stagedFills.Values.OrderBy(f => f.CreatedAt).ToList();
            }
        }
    }

    public int ImportRowsCount
    {
        get
        {
            lock (_gate)
            {
                return _importRows.Count;
            }
        }
    }

    public IReadOnlyList<string> AllowedImportRowDispositions => ImportRowDispositions;

    public bool HasProviderBytesForTest(string uploadId)
    {
        lock (_gate)
        {
            return _providerObjectsByUploadId.ContainsKey(uploadId);
        }
    }

    private UploadTransferResponse BuildTransferResponse(string uploadId)
    {
        UploadRecord upload = _uploads[uploadId];
        return new UploadTransferResponse(
            upload,
            _jobs[upload.ValidateTenantControlJobId],
            _jobs[upload.PurgeTenantControlJobId],
            _uploadObjectLeases[upload.UploadId]);
    }

    private UploadValidationResponse RejectUpload(UploadRecord upload, string safeErrorCode)
    {
        DateTimeOffset now = Now;
        UploadRecord rejected = upload with
        {
            State = "REJECTED",
            SafeErrorCode = safeErrorCode
        };
        _uploads[upload.UploadId] = rejected;
        AddUploadEvent(rejected, "REJECT", "SYSTEM", safeErrorCode, null, now);
        TerminalizeTenantWorkCore(upload.ValidateTenantControlJobId, "UPLOAD_REJECTED");
        return new UploadValidationResponse(rejected, null, safeErrorCode);
    }

    private CsvPreviewData ParseBinanceCsv(byte[] bytes, string fileSha256, long fileSizeBytes)
    {
        if (bytes.LongLength > MaxRawUploadBytes)
        {
            throw new TradeProofException("UPLOAD_TOO_LARGE");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new TradeProofException("UTF8_INVALID", ex.Message);
        }

        using StringReader reader = new(text);
        using TextFieldParser parser = new(reader)
        {
            HasFieldsEnclosedInQuotes = true,
            TextFieldType = FieldType.Delimited,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        string[]? header;
        try
        {
            header = parser.ReadFields();
        }
        catch (MalformedLineException ex)
        {
            throw new TradeProofException("CSV_PARSE_ERROR", ex.Message);
        }

        if (header is null || !header.SequenceEqual(BinanceSpotTradeHistoryHeader, StringComparer.Ordinal))
        {
            throw new TradeProofException("HEADER_MISMATCH");
        }

        int dataRows = 0;
        SortedSet<string> symbols = new(StringComparer.Ordinal);
        DateTimeOffset? firstTradeAt = null;
        DateTimeOffset? lastTradeAt = null;
        try
        {
            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();
                if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                dataRows++;
                if (dataRows > MaxCsvDataRows)
                {
                    throw new TradeProofException("CSV_ROW_LIMIT_EXCEEDED");
                }

                if (fields.Length != BinanceSpotTradeHistoryHeader.Length)
                {
                    throw new TradeProofException("CSV_PARSE_ERROR");
                }

                DateTimeOffset executedAt = ParseCsvTimestamp(fields[0]);
                string symbol = NormalizeSymbol(fields[1]);
                _ = NormalizeSide(fields[2]);
                _ = ContractVersions.CanonicalizeDecimal(fields[3], 20, 18, true);
                _ = ContractVersions.CanonicalizeDecimal(fields[4], 20, 18, true);
                _ = ContractVersions.CanonicalizeDecimal(fields[5], 20, 18, true);
                _ = ParseFee(fields[6]);

                symbols.Add(symbol);
                firstTradeAt = firstTradeAt is null || executedAt < firstTradeAt ? executedAt : firstTradeAt;
                lastTradeAt = lastTradeAt is null || executedAt > lastTradeAt ? executedAt : lastTradeAt;
            }
        }
        catch (MalformedLineException ex)
        {
            throw new TradeProofException("CSV_PARSE_ERROR", ex.Message);
        }

        if (dataRows == 0)
        {
            throw new TradeProofException("CSV_EMPTY");
        }

        return new CsvPreviewData(dataRows, symbols.ToList(), firstTradeAt, lastTradeAt, fileSha256, fileSizeBytes);
    }

    private static DateTimeOffset ParseCsvTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new TradeProofException("CSV_PARSE_ERROR");
        }

        return parsed;
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
        {
            throw new TradeProofException("TIMESTAMP_INVALID");
        }

        return parsed.ToUniversalTime();
    }

    private static (string Quantity, string Asset) ParseFee(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new TradeProofException("CSV_PARSE_ERROR");
        }

        return (ContractVersions.CanonicalizeDecimal(parts[0], 20, 18, false), NormalizeAsset(parts[1]));
    }

    private static string NormalizeSymbol(string symbol)
    {
        string normalized = symbol.Trim().ToUpperInvariant();
        if (!normalized.EndsWith("USDT", StringComparison.Ordinal) || normalized.Length < 5 || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            throw new TradeProofException("SYMBOL_UNSUPPORTED");
        }

        return normalized;
    }

    private static string NormalizeAsset(string asset)
    {
        string normalized = asset.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 12 || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            throw new TradeProofException("ASSET_UNSUPPORTED");
        }

        return normalized;
    }

    private static string NormalizeSide(string side)
    {
        string normalized = side.Trim().ToUpperInvariant();
        if (normalized is not ("BUY" or "SELL"))
        {
            throw new TradeProofException("SIDE_UNSUPPORTED");
        }

        return normalized;
    }

    private void AddReservationEvent(ObjectIngestReservationRecord reservation, string eventType, string? safeReasonCode, DateTimeOffset recordedAt)
    {
        List<ObjectIngestReservationEventRecord> events = _objectIngestReservationEvents[reservation.ObjectIngestReservationId];
        events.Add(new ObjectIngestReservationEventRecord(
            NextId("oirevt"),
            reservation.ObjectIngestReservationId,
            reservation.WorkspaceId,
            events.Count + 1,
            eventType,
            recordedAt,
            safeReasonCode));
    }

    private void AddUploadEvent(UploadRecord upload, string eventType, string actorType, string? safeReasonCode, string? absenceVerificationId, DateTimeOffset recordedAt)
    {
        List<UploadStateEventRecord> events = _uploadEvents[upload.UploadId];
        events.Add(new UploadStateEventRecord(
            NextId("uplevt"),
            upload.WorkspaceId,
            upload.UploadId,
            events.Count + 1,
            eventType,
            recordedAt,
            actorType,
            safeReasonCode,
            absenceVerificationId));
    }

    private ObjectIngestReservationRecord GetOwnedReservation(ActorContext actor, string reservationId)
    {
        if (!_objectIngestReservations.TryGetValue(reservationId, out ObjectIngestReservationRecord? reservation) || reservation.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("OBJECT_INGEST_RESERVATION_NOT_FOUND");
        }

        return reservation;
    }

    private UploadRecord GetOwnedUpload(ActorContext actor, string uploadId)
    {
        if (!_uploads.TryGetValue(uploadId, out UploadRecord? upload) || upload.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("UPLOAD_NOT_FOUND");
        }

        return upload;
    }

    private static string ObjectIngestSubject(string reservationId) =>
        JsonSerializer.Serialize(new { object_ingest_reservation_id = reservationId }, ContractVersions.JsonOptions);

    private static string UploadSubject(string uploadId) =>
        JsonSerializer.Serialize(new { upload_id = uploadId }, ContractVersions.JsonOptions);

    private static string ImportBatchSubject(string batchId) =>
        JsonSerializer.Serialize(new { import_batch_id = batchId }, ContractVersions.JsonOptions);

    private static void EnsureIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new TradeProofException("IDEMPOTENCY_KEY_REQUIRED");
        }
    }

    private static string BuildWriteCapability(string workspaceId, string reservationId, string providerObjectKeySha256, int leaseGeneration, DateTimeOffset writeExpiresAt)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            leaseGeneration,
            objectIngestReservationId = reservationId,
            providerObjectKeySha256,
            workspaceId,
            writeExpiresAt
        }, ContractVersions.JsonOptions)));
        return "oirw_" + Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record ProviderObjectVersion(
        string ObjectIngestReservationId,
        string UploadId,
        string ProviderObjectVersionId,
        string ContentSha256,
        long SizeBytes,
        byte[] Bytes,
        DateTimeOffset CreatedAt);

    private sealed record CsvPreviewData(
        int DataRows,
        IReadOnlyList<string> Symbols,
        DateTimeOffset? FirstTradeAt,
        DateTimeOffset? LastTradeAt,
        string FileSha256,
        long FileSizeBytes);
}
