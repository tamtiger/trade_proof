using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public sealed partial class TradeProofApp
{
    private readonly Dictionary<string, ImportRowRecord> _importRows = [];
    private readonly Dictionary<string, NormalizedFillRecord> _normalizedFills = [];
    private readonly Dictionary<string, string> _normalizedFillByDedupKey = [];
    private readonly Dictionary<string, FeeConversionRecord> _feeConversions = [];
    private readonly Dictionary<string, TradeEpisodeHeaderRecord> _tradeEpisodes = [];
    private readonly Dictionary<string, List<TradeEpisodeProjectionRecord>> _episodeProjections = [];
    private readonly List<EpisodeFillAllocationRecord> _episodeAllocations = [];
    private readonly List<AccountingLedgerEntryRecord> _accountingLedgerEntries = [];

    public Task<CommandResult<ImportBatchRecord>> ProcessImportAsync(
        ActorContext actor,
        ProcessImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ProcessImport", request.IdempotencyKey, request, () =>
                    ProcessImportCore(actor, request.ImportBatchId)));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ImportBatchRecord>.Fail(ex.Code));
            }
        }
    }

    public IReadOnlyList<ImportRowRecord> ImportRows
    {
        get
        {
            lock (_gate)
            {
                return _importRows.Values.OrderBy(r => r.ImportBatchId, StringComparer.Ordinal).ThenBy(r => r.SourceRowNumber).ToList();
            }
        }
    }

    public IReadOnlyList<NormalizedFillRecord> NormalizedFills
    {
        get
        {
            lock (_gate)
            {
                return _normalizedFills.Values.OrderBy(f => f.SourceTimeStart).ThenBy(f => f.SourceRowNumber).ToList();
            }
        }
    }

    public IReadOnlyList<FeeConversionRecord> FeeConversions
    {
        get
        {
            lock (_gate)
            {
                return _feeConversions.Values.OrderBy(c => c.CreatedAt).ThenBy(c => c.FeeConversionId, StringComparer.Ordinal).ToList();
            }
        }
    }

    public IReadOnlyList<TradeEpisodeProjectionRecord> TradeEpisodeProjections
    {
        get
        {
            lock (_gate)
            {
                return ActiveEpisodeProjections()
                    .OrderBy(p => p.FirstFillAt)
                    .ThenBy(p => p.EpisodeId, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public IReadOnlyList<AccountingLedgerEntryRecord> AccountingLedgerEntries
    {
        get
        {
            lock (_gate)
            {
                HashSet<string> activeProjectionKeys = ActiveEpisodeProjections()
                    .Select(p => ProjectionKey(p.EpisodeId, p.ProjectionVersion))
                    .ToHashSet(StringComparer.Ordinal);
                return _accountingLedgerEntries
                    .Where(e => activeProjectionKeys.Contains(ProjectionKey(e.EpisodeId, e.ProjectionVersion)))
                    .OrderBy(e => e.EpisodeId, StringComparer.Ordinal)
                    .ThenBy(e => e.EntrySequence)
                    .ToList();
            }
        }
    }

    private ImportBatchRecord ProcessImportCore(ActorContext actor, string importBatchId)
    {
        if (!_importBatches.TryGetValue(importBatchId, out ImportBatchRecord? batch) || batch.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("IMPORT_BATCH_NOT_FOUND");
        }

        if (batch.Status is "COMPLETE" or "PARTIAL" or "NEEDS_ATTENTION" or "REJECTED")
        {
            return batch;
        }

        if (batch.Status is not ("UPLOADED" or "PROCESSING"))
        {
            throw new TradeProofException("IMPORT_BATCH_NOT_PROCESSABLE");
        }

        if (!_uploads.TryGetValue(batch.SourceUploadId, out UploadRecord? upload) || upload.WorkspaceId != actor.WorkspaceId)
        {
            return RejectImportBatch(batch, "SOURCE_UPLOAD_NOT_FOUND");
        }

        if (!_providerObjectsByUploadId.TryGetValue(upload.UploadId, out var providerObject))
        {
            return RejectImportBatch(batch, "PROVIDER_OBJECT_NOT_FOUND");
        }

        List<ParsedImportCsvRow> parsedRows;
        try
        {
            parsedRows = ParseImportCsvRows(providerObject.Bytes);
        }
        catch (TradeProofException ex)
        {
            return RejectImportBatch(batch, ex.Code);
        }

        if (parsedRows.Count != batch.DataRows)
        {
            return RejectImportBatch(batch, "IMPORT_SOURCE_REVALIDATION_MISMATCH");
        }

        ImportBatchRecord processing = batch with { Status = "PROCESSING" };
        _importBatches[batch.ImportBatchId] = processing;

        foreach (ParsedImportCsvRow row in parsedRows.OrderBy(r => r.SourceTimeStart).ThenBy(r => r.SourceRowNumber))
        {
            ProcessImportRow(actor, processing, row);
        }

        int reconciled = _importRows.Values.Count(r => r.ImportBatchId == batch.ImportBatchId && r.Status == "RECONCILED");
        int duplicate = _importRows.Values.Count(r => r.ImportBatchId == batch.ImportBatchId && r.Status == "DUPLICATE");
        int pending = _importRows.Values.Count(r => r.ImportBatchId == batch.ImportBatchId && r.Status == "ACCOUNTING_PENDING");
        int quarantined = _importRows.Values.Count(r => r.ImportBatchId == batch.ImportBatchId && r.Status == "QUARANTINED");
        int denominator = reconciled + duplicate + pending + quarantined;
        string status = DetermineBatchStatus(reconciled + duplicate, denominator, pending, quarantined);
        ImportBatchRecord terminal = processing with
        {
            Status = status,
            DataRows = denominator,
            ReconciledRows = reconciled,
            DuplicateRows = duplicate,
            AccountingPendingRows = pending,
            QuarantinedRows = quarantined,
            FileErrorCode = null
        };
        _importBatches[batch.ImportBatchId] = terminal;
        TerminalizeTenantWorkCore(batch.ImportTenantControlJobId, status == "COMPLETE" ? "IMPORT_RECONCILED" : "IMPORT_NEEDS_ATTENTION");
        return terminal;
    }

    private ImportBatchRecord RejectImportBatch(ImportBatchRecord batch, string safeErrorCode)
    {
        ImportBatchRecord rejected = batch with
        {
            Status = "REJECTED",
            DataRows = null,
            ReconciledRows = 0,
            DuplicateRows = 0,
            AccountingPendingRows = 0,
            QuarantinedRows = 0,
            FileErrorCode = safeErrorCode
        };
        _importBatches[batch.ImportBatchId] = rejected;
        TerminalizeTenantWorkCore(batch.ImportTenantControlJobId, "IMPORT_REJECTED");
        return rejected;
    }

    private void ProcessImportRow(ActorContext actor, ImportBatchRecord batch, ParsedImportCsvRow row)
    {
        if (_importRows.Values.Any(existing => existing.ImportBatchId == batch.ImportBatchId && existing.SourceRowNumber == row.SourceRowNumber))
        {
            return;
        }

        string importRowId = NextId("improw");
        string rawRowSha256 = ContractVersions.Sha256Utf8(string.Join('\u001F', row.RawFields));
        string dedupScope = DedupScope(actor.WorkspaceId, batch.TradingAccountId, row.CanonicalSignatureSha256);
        if (_normalizedFillByDedupKey.TryGetValue(dedupScope, out string? existingFillId))
        {
            AddImportRow(batch, importRowId, row, rawRowSha256, "DUPLICATE", null, null, existingFillId, null);
            return;
        }

        TradeEpisodeProjectionRecord? openProjection = FindOpenProjection(actor.WorkspaceId, batch.TradingAccountId, row.InstrumentId);
        if (row.Side == "SELL")
        {
            if (openProjection is null)
            {
                AddImportRow(batch, importRowId, row, rawRowSha256, "QUARANTINED", "SELL_WITHOUT_OPEN_POSITION", null, null, null);
                return;
            }

            if (row.ExecutedQtyBase > ParseStoredDecimal(openProjection.PositionQtyBase))
            {
                AddImportRow(batch, importRowId, row, rawRowSha256, "QUARANTINED", "SELL_EXCEEDS_POSITION", null, null, null);
                return;
            }
        }

        if (row.Side == "BUY" && row.FeeAsset == row.BaseAsset && row.FeeQty >= row.ExecutedQtyBase)
        {
            AddImportRow(batch, importRowId, row, rawRowSha256, "QUARANTINED", "BUY_BASE_FEE_EXCEEDS_EXECUTED", null, null, null);
            return;
        }

        NormalizedFillRecord fill = CreateNormalizedFill(batch, importRowId, row);
        _normalizedFills[fill.FillId] = fill;
        _normalizedFillByDedupKey[dedupScope] = fill.FillId;

        FeeConversionRecord conversion = CreateFeeConversion(fill);
        _feeConversions[conversion.FeeConversionId] = conversion;
        PublishEpisodeProjection(actor, fill);

        string status = conversion.Status == "UNAVAILABLE" ? "ACCOUNTING_PENDING" : "RECONCILED";
        string? safeErrorCode = conversion.Status == "UNAVAILABLE" ? "FEE_CONVERSION_MISSING" : null;
        AddImportRow(batch, importRowId, row, rawRowSha256, status, safeErrorCode, fill.FillId, null, null);
    }

    private NormalizedFillRecord CreateNormalizedFill(ImportBatchRecord batch, string importRowId, ParsedImportCsvRow row)
    {
        int occurrenceIndex = _normalizedFills.Values.Count(f =>
            f.WorkspaceId == batch.WorkspaceId &&
            f.TradingAccountId == batch.TradingAccountId &&
            f.CanonicalSignatureSha256 == row.CanonicalSignatureSha256) + 1;

        return new NormalizedFillRecord(
            NextId("fill"),
            batch.WorkspaceId,
            batch.TradingAccountId,
            batch.ImportBatchId,
            importRowId,
            row.SourceRowNumber,
            ContractVersions.BinanceSpotTradeHistoryCsv,
            ContractVersions.NormalizedFill,
            ImportCatalogVersion,
            "BINANCE",
            "SPOT",
            row.InstrumentId,
            row.VenueSymbol,
            row.BaseAsset,
            "USDT",
            row.Side,
            row.SourceTimeStart,
            row.SourceTimestampPrecision,
            row.SourceTimeStart,
            row.SourceTimeEndExclusive,
            CanonicalDecimal(row.PriceQuotePerBase),
            CanonicalDecimal(row.ExecutedQtyBase),
            CanonicalDecimal(row.GrossAmountQuote),
            CanonicalDecimal(row.FeeQty),
            row.FeeAsset,
            row.CanonicalSignatureSha256,
            occurrenceIndex,
            row.CanonicalSignatureSha256,
            Now);
    }

    private FeeConversionRecord CreateFeeConversion(NormalizedFillRecord fill)
    {
        decimal feeQty = ParseStoredDecimal(fill.FeeQty);
        string status;
        string? method;
        string? rate;
        string? value;
        DateTimeOffset? asOfAt;
        string? marketBarIdsJson = null;
        string? marketBarSourceObservationIdsJson = null;
        string? marketConversionCatalogVersion = null;
        string? conversionPathJson = null;

        if (feeQty == 0)
        {
            status = "EXACT";
            method = null;
            rate = null;
            value = "0";
            asOfAt = null;
        }
        else if (fill.FeeAsset == fill.QuoteAsset)
        {
            status = "EXACT";
            method = "NATIVE_QUOTE";
            rate = "1";
            value = CanonicalDecimal(feeQty);
            asOfAt = null;
        }
        else if (fill.FeeAsset == fill.BaseAsset)
        {
            status = "EXACT";
            method = "FILL_RATE";
            decimal fillRate = RoundScale18(ParseStoredDecimal(fill.GrossAmountQuote) / ParseStoredDecimal(fill.ExecutedQtyBase));
            rate = CanonicalDecimal(fillRate);
            value = CanonicalDecimal(RoundScale18(feeQty * fillRate));
            asOfAt = null;
        }
        else
        {
            MarketFeeConversionResolution? marketResolution = ResolveMarketFeeConversion(fill, feeQty);
            if (marketResolution is null)
            {
                status = "UNAVAILABLE";
                method = null;
                rate = null;
                value = null;
                asOfAt = null;
            }
            else
            {
                status = "DERIVED";
                method = marketResolution.Method;
                rate = marketResolution.Rate;
                value = marketResolution.Value;
                asOfAt = marketResolution.AsOfAt;
                marketBarIdsJson = marketResolution.MarketBarIdsJson;
                marketBarSourceObservationIdsJson = marketResolution.MarketBarSourceObservationIdsJson;
                marketConversionCatalogVersion = marketResolution.MarketConversionCatalogVersion;
                conversionPathJson = marketResolution.ConversionPathJson;
            }
        }

        return new FeeConversionRecord(
            NextId("feeconv"),
            fill.WorkspaceId,
            fill.FillId,
            1,
            fill.FeeAsset,
            fill.QuoteAsset,
            fill.FeeQty,
            status,
            method,
            rate,
            value,
            asOfAt,
            marketBarIdsJson,
            marketBarSourceObservationIdsJson,
            marketConversionCatalogVersion,
            conversionPathJson,
            ContractVersions.FeeConversion,
            Now,
            null);
    }

    private void PublishEpisodeProjection(ActorContext actor, NormalizedFillRecord fill)
    {
        TradeEpisodeProjectionRecord? currentOpen = FindOpenProjection(fill.WorkspaceId, fill.TradingAccountId, fill.InstrumentId);
        if (fill.Side == "SELL" && currentOpen is null)
        {
            throw new TradeProofException("SELL_WITHOUT_OPEN_POSITION");
        }

        string episodeId;
        List<NormalizedFillRecord> fills;
        TradeEpisodeProjectionRecord? priorProjection = null;
        PlanProofSnapshot proof;
        DateTimeOffset createdAt = Now;

        if (currentOpen is null)
        {
            episodeId = NextId("episode");
            TradeEpisodeHeaderRecord header = new(
                episodeId,
                fill.WorkspaceId,
                fill.TradingAccountId,
                fill.InstrumentId,
                fill.FillId,
                fill.DedupKey,
                createdAt);
            _tradeEpisodes[episodeId] = header;
            _episodeProjections[episodeId] = [];
            fills = [fill];
            proof = EvaluatePlanProof(actor, fill, episodeId, createdAt);
        }
        else
        {
            priorProjection = currentOpen;
            episodeId = currentOpen.EpisodeId;
            fills = ActiveAllocationFillIds(currentOpen)
                .Select(id => _normalizedFills[id])
                .Concat([fill])
                .OrderBy(f => f.SourceTimeStart)
                .ThenBy(f => f.SourceRowNumber)
                .ToList();
            proof = PlanProofSnapshot.FromProjection(currentOpen);
        }

        int projectionVersion = (priorProjection?.ProjectionVersion ?? 0) + 1;
        ProjectionBuildResult build = BuildProjection(actor.WorkspaceId, episodeId, projectionVersion, fills, proof, createdAt);
        if (priorProjection is not null)
        {
            SupersedeProjection(priorProjection, createdAt);
        }

        _episodeProjections[episodeId].Add(build.Projection);
        _episodeAllocations.AddRange(build.Allocations);
        _accountingLedgerEntries.AddRange(build.LedgerEntries);
        EnqueueContextForProjection(actor, build.Projection);
    }

    private ProjectionBuildResult BuildProjection(
        string workspaceId,
        string episodeId,
        int projectionVersion,
        IReadOnlyList<NormalizedFillRecord> fills,
        PlanProofSnapshot proof,
        DateTimeOffset createdAt)
    {
        decimal quantity = 0;
        decimal basis = 0;
        decimal gross = 0;
        decimal knownFee = 0;
        bool hasMissingFee = false;
        List<EpisodeFillAllocationRecord> allocations = [];
        List<AccountingLedgerEntryRecord> ledgerEntries = [];

        for (int i = 0; i < fills.Count; i++)
        {
            NormalizedFillRecord fill = fills[i];
            FeeConversionRecord conversion = _feeConversions.Values.Single(c => c.FillId == fill.FillId && c.SupersededAt is null);
            decimal fillQty = ParseStoredDecimal(fill.ExecutedQtyBase);
            decimal fillAmount = ParseStoredDecimal(fill.GrossAmountQuote);
            decimal feeQty = ParseStoredDecimal(fill.FeeQty);
            decimal? feeValue = conversion.FeeValueQuote is null ? null : ParseStoredDecimal(conversion.FeeValueQuote);
            decimal beforeQuantity = quantity;
            decimal beforeBasis = basis;
            decimal quantityDelta;
            decimal basisDelta;
            decimal grossDelta;

            if (fill.Side == "BUY")
            {
                if (fill.FeeAsset == fill.BaseAsset)
                {
                    decimal nonNullFee = feeValue ?? throw new TradeProofException("BUY_BASE_FEE_CONVERSION_MISSING");
                    quantityDelta = fillQty - feeQty;
                    basisDelta = fillAmount - nonNullFee;
                }
                else
                {
                    quantityDelta = fillQty;
                    basisDelta = fillAmount;
                }

                quantity += quantityDelta;
                basis += basisDelta;
                grossDelta = 0;
            }
            else
            {
                if (fillQty > quantity)
                {
                    throw new TradeProofException("SELL_EXCEEDS_POSITION");
                }

                decimal costRemoved = fillQty == quantity ? basis : RoundScale18((basis / quantity) * fillQty);
                quantityDelta = -fillQty;
                basisDelta = -costRemoved;
                quantity += quantityDelta;
                basis += basisDelta;
                grossDelta = fillAmount - costRemoved;
                gross += grossDelta;
                if (quantity == 0)
                {
                    basis = 0;
                }
            }

            if (feeValue is null)
            {
                hasMissingFee = true;
            }
            else
            {
                knownFee += feeValue.Value;
            }

            EpisodeFillAllocationRecord allocation = new(
                episodeId,
                workspaceId,
                projectionVersion,
                fill.FillId,
                i + 1,
                CanonicalDecimal(beforeQuantity),
                CanonicalDecimal(quantityDelta),
                CanonicalDecimal(quantity),
                CanonicalDecimal(beforeBasis),
                CanonicalDecimal(basisDelta),
                CanonicalDecimal(basis),
                CanonicalDecimal(grossDelta),
                feeValue is null ? null : CanonicalDecimal(feeValue.Value));
            allocations.Add(allocation);
            ledgerEntries.AddRange(BuildLedgerEntries(episodeId, projectionVersion, allocation, fill, conversion, createdAt));
        }

        NormalizedFillRecord firstFill = fills[0];
        NormalizedFillRecord? closingFill = quantity == 0 ? fills[^1] : null;
        decimal? net = hasMissingFee ? null : gross - knownFee;
        string? plannedRisk = proof.FrozenPlanRevisionId is null ? null : FindPlanRevision(proof.FrozenPlanRevisionId)?.PlannedRiskUsdt;
        string? rMultiple = plannedRisk is not null && net is not null && quantity == 0
            ? CanonicalDecimal(RoundScale18(net.Value / ParseStoredDecimal(plannedRisk)))
            : null;

        TradeEpisodeProjectionRecord projection = new(
            episodeId,
            projectionVersion,
            ContractVersions.EpisodeProjection,
            ContractVersions.WeightedAverageEpisode,
            workspaceId,
            firstFill.TradingAccountId,
            firstFill.InstrumentId,
            "USDT",
            quantity == 0 ? "CLOSED" : "OPEN",
            firstFill.FillId,
            firstFill.SourceTimeStart,
            firstFill.SourceTimeEndExclusive,
            firstFill.SourceTimestampPrecision,
            closingFill?.FillId,
            closingFill?.SourceTimeStart,
            closingFill?.SourceTimeEndExclusive,
            closingFill?.SourceTimestampPrecision,
            proof.AssociatedPlanId,
            proof.AssociatedPlanRevisionId,
            proof.FrozenPlanRevisionId,
            proof.Status,
            proof.ReasonCode,
            ContractVersions.PlanProof,
            proof.CandidateIdsJson,
            proof.BasisJson,
            proof.ResolvedAt,
            null,
            null,
            CanonicalDecimal(quantity),
            CanonicalDecimal(basis),
            quantity == 0 ? null : CanonicalDecimal(RoundScale18(basis / quantity)),
            CanonicalDecimal(gross),
            CanonicalDecimal(knownFee),
            net is null ? null : CanonicalDecimal(net.Value),
            plannedRisk,
            rMultiple,
            hasMissingFee ? "FEE_CONVERSION_MISSING" : "COMPLETE",
            createdAt,
            null);
        return new ProjectionBuildResult(projection, allocations, ledgerEntries);
    }

    private IEnumerable<AccountingLedgerEntryRecord> BuildLedgerEntries(
        string episodeId,
        int projectionVersion,
        EpisodeFillAllocationRecord allocation,
        NormalizedFillRecord fill,
        FeeConversionRecord conversion,
        DateTimeOffset createdAt)
    {
        decimal fillQty = ParseStoredDecimal(fill.ExecutedQtyBase);
        decimal fillAmount = ParseStoredDecimal(fill.GrossAmountQuote);
        decimal feeQty = ParseStoredDecimal(fill.FeeQty);
        decimal? feeValue = conversion.FeeValueQuote is null ? null : ParseStoredDecimal(conversion.FeeValueQuote);
        bool buy = fill.Side == "BUY";
        int tradeSequence = allocation.EventSequence * 2 - 1;
        int feeSequence = allocation.EventSequence * 2;
        string tradeAssetQty = CanonicalDecimal(buy ? fillQty : -fillQty);
        string tradeQuoteValue = CanonicalDecimal(buy ? -fillAmount : fillAmount);

        yield return new AccountingLedgerEntryRecord(
            NextId("ledger"),
            fill.WorkspaceId,
            episodeId,
            projectionVersion,
            fill.FillId,
            tradeSequence,
            "TRADE",
            fill.SourceTimeStart,
            fill.BaseAsset,
            tradeAssetQty,
            fill.QuoteAsset,
            tradeQuoteValue,
            allocation.PositionQtyDelta,
            allocation.CostBasisDelta,
            allocation.GrossRealizedDeltaQuote,
            "0",
            null,
            ContractVersions.WeightedAverageEpisode,
            createdAt);

        decimal feePositionDelta = buy && fill.FeeAsset == fill.BaseAsset ? -feeQty : 0;
        decimal feeBasisDelta = buy && fill.FeeAsset == fill.BaseAsset && feeValue is not null ? -feeValue.Value : 0;
        yield return new AccountingLedgerEntryRecord(
            NextId("ledger"),
            fill.WorkspaceId,
            episodeId,
            projectionVersion,
            fill.FillId,
            feeSequence,
            "FEE",
            fill.SourceTimeStart,
            fill.FeeAsset,
            CanonicalDecimal(-feeQty),
            fill.QuoteAsset,
            feeValue is null ? null : CanonicalDecimal(-feeValue.Value),
            CanonicalDecimal(feePositionDelta),
            CanonicalDecimal(feeBasisDelta),
            "0",
            feeValue is null ? null : CanonicalDecimal(feeValue.Value),
            conversion.FeeConversionId,
            ContractVersions.WeightedAverageEpisode,
            createdAt);
    }

    private PlanProofSnapshot EvaluatePlanProof(ActorContext actor, NormalizedFillRecord firstFill, string episodeId, DateTimeOffset evaluatedAt)
    {
        List<(TradePlanHeaderRecord Plan, TradePlanRevisionRecord Revision)> candidates = _plans.Values
            .Where(p => p.WorkspaceId == actor.WorkspaceId &&
                        p.TradingAccountId == firstFill.TradingAccountId &&
                        p.Symbol == firstFill.VenueSymbol &&
                        p.CreatedAt < firstFill.SourceTimeEndExclusive &&
                        p.ExpiresAt >= firstFill.SourceTimeStart &&
                        p.State == "ARMED")
            .Select(p => (Plan: p, Revision: _planRevisions[p.TradePlanId]
                .Where(r => r.SubmittedAt < firstFill.SourceTimeEndExclusive)
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.RevisionNo)
                .FirstOrDefault()))
            .Where(p => p.Revision is not null)
            .Select(p => (p.Plan, p.Revision!))
            .OrderBy(p => p.Plan.TradePlanId, StringComparer.Ordinal)
            .ToList();

        string candidateIdsJson = BuildCandidateIdsJson(candidates);
        string basisJson = BuildPlanProofBasisJson(firstFill, evaluatedAt, candidates);
        if (candidates.Count == 0)
        {
            return new PlanProofSnapshot(
                "UNMATCHED",
                "NO_ELIGIBLE_CANDIDATE",
                null,
                null,
                null,
                candidateIdsJson,
                basisJson,
                evaluatedAt);
        }

        if (candidates.Count > 1)
        {
            return new PlanProofSnapshot(
                "AMBIGUOUS",
                "MULTIPLE_CANDIDATES",
                null,
                null,
                null,
                candidateIdsJson,
                basisJson,
                evaluatedAt);
        }

        (TradePlanHeaderRecord plan, TradePlanRevisionRecord revision) = candidates[0];
        bool armInside = plan.CreatedAt >= firstFill.SourceTimeStart && plan.CreatedAt < firstFill.SourceTimeEndExclusive;
        bool revisionInside = revision.SubmittedAt >= firstFill.SourceTimeStart && revision.SubmittedAt < firstFill.SourceTimeEndExclusive;
        bool expiryInside = plan.ExpiresAt >= firstFill.SourceTimeStart && plan.ExpiresAt < firstFill.SourceTimeEndExclusive;
        string status = armInside || revisionInside || expiryInside ? "AMBIGUOUS" : "VERIFIED";
        string reason = status == "VERIFIED"
            ? "VERIFIED_BEFORE_INTERVAL"
            : armInside
                ? "ARM_INSIDE_INTERVAL"
                : revisionInside
                    ? "REVISION_INSIDE_INTERVAL"
                    : "EXPIRY_INSIDE_INTERVAL";

        ConsumePlanIfArmed(plan, actor.WorkspaceId, episodeId);
        return new PlanProofSnapshot(
            status,
            reason,
            plan.TradePlanId,
            revision.TradePlanRevisionId,
            status == "VERIFIED" ? revision.TradePlanRevisionId : null,
            candidateIdsJson,
            basisJson,
            evaluatedAt);
    }

    private void ConsumePlanIfArmed(TradePlanHeaderRecord plan, string workspaceId, string episodeId)
    {
        if (!_plans.TryGetValue(plan.TradePlanId, out TradePlanHeaderRecord? current) || current.State != "ARMED")
        {
            return;
        }

        _plans[plan.TradePlanId] = current with { State = "CONSUMED" };
        AddPlanEvent(plan.TradePlanId, workspaceId, "CONSUME", Now);
    }

    private void AddImportRow(
        ImportBatchRecord batch,
        string importRowId,
        ParsedImportCsvRow row,
        string rawRowSha256,
        string status,
        string? safeErrorCode,
        string? normalizedFillId,
        string? duplicateOfFillId,
        string? stagedFillId)
    {
        _importRows[importRowId] = new ImportRowRecord(
            importRowId,
            batch.WorkspaceId,
            batch.TradingAccountId,
            batch.ImportBatchId,
            row.SourceRowNumber,
            rawRowSha256,
            status,
            safeErrorCode,
            normalizedFillId,
            duplicateOfFillId,
            stagedFillId,
            Now);
    }

    private List<ParsedImportCsvRow> ParseImportCsvRows(byte[] bytes)
    {
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

        List<ParsedImportCsvRow> rows = [];
        int sourceRowNumber = 1;
        try
        {
            while (!parser.EndOfData)
            {
                sourceRowNumber++;
                string[]? fields = parser.ReadFields();
                if (fields is null || fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (fields.Length != BinanceSpotTradeHistoryHeader.Length)
                {
                    throw new TradeProofException("CSV_PARSE_ERROR");
                }

                rows.Add(ParseImportCsvRow(sourceRowNumber, fields));
            }
        }
        catch (MalformedLineException ex)
        {
            throw new TradeProofException("CSV_PARSE_ERROR", ex.Message);
        }

        return rows;
    }

    private ParsedImportCsvRow ParseImportCsvRow(int sourceRowNumber, string[] fields)
    {
        (DateTimeOffset sourceStart, DateTimeOffset sourceEnd, string precision) = ParseCsvTimestampInterval(fields[0]);
        string symbol = NormalizeSymbol(fields[1]);
        string side = NormalizeSide(fields[2]);
        decimal price = ParseSourceDecimal(fields[3], true);
        decimal quantity = ParseSourceDecimal(fields[4], true);
        decimal gross = ParseSourceDecimal(fields[5], true);
        (decimal feeQty, string feeAsset) = ParseFeeAmount(fields[6]);
        string baseAsset = symbol[..^4];
        if (Math.Abs(gross - price * quantity) > 0.00000001m)
        {
            throw new TradeProofException("AMOUNT_PRICE_MISMATCH");
        }

        string canonicalSignature = ContractVersions.Sha256CanonicalJson(new
        {
            executedAt = sourceStart,
            fee = CanonicalDecimal(feeQty),
            feeAsset,
            gross = CanonicalDecimal(gross),
            price = CanonicalDecimal(price),
            quantity = CanonicalDecimal(quantity),
            side,
            symbol,
            venue = "BINANCE"
        });
        return new ParsedImportCsvRow(
            sourceRowNumber,
            fields,
            symbol,
            $"inst_{symbol.ToLowerInvariant()}",
            baseAsset,
            side,
            sourceStart,
            sourceEnd,
            precision,
            price,
            quantity,
            gross,
            feeQty,
            feeAsset,
            canonicalSignature);
    }

    private static (DateTimeOffset Start, DateTimeOffset EndExclusive, string Precision) ParseCsvTimestampInterval(string value)
    {
        if (DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset millis))
        {
            DateTimeOffset start = millis.ToUniversalTime();
            return (start, start.AddMilliseconds(1), "MILLISECOND");
        }

        if (DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset seconds))
        {
            DateTimeOffset start = seconds.ToUniversalTime();
            return (start, start.AddSeconds(1), "SECOND");
        }

        throw new TradeProofException("CSV_PARSE_ERROR");
    }

    private static decimal ParseSourceDecimal(string value, bool requirePositive) =>
        ParseStoredDecimal(ContractVersions.CanonicalizeDecimal(value, 20, 18, requirePositive));

    private static (decimal Quantity, string Asset) ParseFeeAmount(string value)
    {
        string trimmed = value.Trim();
        int split = 0;
        while (split < trimmed.Length && (char.IsDigit(trimmed[split]) || trimmed[split] == '.'))
        {
            split++;
        }

        string quantityText = trimmed[..split].Trim();
        string asset = trimmed[split..].Trim();
        if (quantityText.Length == 0 || asset.Length == 0)
        {
            throw new TradeProofException("CSV_PARSE_ERROR");
        }

        return (ParseStoredDecimal(ContractVersions.CanonicalizeDecimal(quantityText, 20, 18, false)), NormalizeAsset(asset));
    }

    private TradeEpisodeProjectionRecord? FindOpenProjection(string workspaceId, string tradingAccountId, string instrumentId) =>
        ActiveEpisodeProjections().SingleOrDefault(p =>
            p.WorkspaceId == workspaceId &&
            p.TradingAccountId == tradingAccountId &&
            p.InstrumentId == instrumentId &&
            p.State == "OPEN");

    private IEnumerable<TradeEpisodeProjectionRecord> ActiveEpisodeProjections() =>
        _episodeProjections.Values.SelectMany(projections => projections).Where(p => p.SupersededAt is null);

    private IReadOnlyList<string> ActiveAllocationFillIds(TradeEpisodeProjectionRecord projection) =>
        _episodeAllocations
            .Where(a => a.EpisodeId == projection.EpisodeId && a.ProjectionVersion == projection.ProjectionVersion)
            .OrderBy(a => a.EventSequence)
            .Select(a => a.FillId)
            .ToList();

    private void SupersedeProjection(TradeEpisodeProjectionRecord projection, DateTimeOffset supersededAt)
    {
        List<TradeEpisodeProjectionRecord> projections = _episodeProjections[projection.EpisodeId];
        int index = projections.FindIndex(p => p.ProjectionVersion == projection.ProjectionVersion);
        if (index >= 0)
        {
            projections[index] = projection with { SupersededAt = supersededAt };
        }
    }

    private IReadOnlyList<ImportEpisodeSummaryRecord> BuildImportEpisodeSummaries(string importBatchId)
    {
        HashSet<string> batchFillIds = _normalizedFills.Values
            .Where(f => f.ImportBatchId == importBatchId)
            .Select(f => f.FillId)
            .ToHashSet(StringComparer.Ordinal);
        return ActiveEpisodeProjections()
            .Where(p => ActiveAllocationFillIds(p).Any(batchFillIds.Contains))
            .Select(p =>
            {
                NormalizedFillRecord firstFill = _normalizedFills[p.FirstFillId];
                return new ImportEpisodeSummaryRecord(
                    p.EpisodeId,
                    p.ProjectionVersion,
                    p.State,
                    firstFill.VenueSymbol,
                    p.PlanProofStatus,
                    p.AccountingQuality,
                    p.GrossRealizedPnlQuote,
                    p.NetRealizedPnlQuote,
                    p.RMultiple);
            })
            .OrderBy(e => e.EpisodeId, StringComparer.Ordinal)
            .ToList();
    }

    private TradePlanRevisionRecord? FindPlanRevision(string revisionId) =>
        _planRevisions.Values.SelectMany(revisions => revisions).SingleOrDefault(r => r.TradePlanRevisionId == revisionId);

    private static string BuildCandidateIdsJson(IReadOnlyList<(TradePlanHeaderRecord Plan, TradePlanRevisionRecord Revision)> candidates) =>
        JsonSerializer.Serialize(candidates.Select(candidate => new
        {
            planRecordKey = new { trade_plan_id = candidate.Plan.TradePlanId },
            revisionRecordKey = new { trade_plan_revision_id = candidate.Revision.TradePlanRevisionId }
        }), ContractVersions.JsonOptions);

    private static string BuildPlanProofBasisJson(
        NormalizedFillRecord firstFill,
        DateTimeOffset evaluatedAt,
        IReadOnlyList<(TradePlanHeaderRecord Plan, TradePlanRevisionRecord Revision)> candidates) =>
        JsonSerializer.Serialize(new
        {
            evaluatedAt,
            evaluatedPlans = candidates.Select(candidate => new
            {
                armRecordedAt = candidate.Plan.CreatedAt,
                candidate = true,
                expiresAt = candidate.Plan.ExpiresAt,
                planRecordKey = new { trade_plan_id = candidate.Plan.TradePlanId },
                revisionRecordKey = new { trade_plan_revision_id = candidate.Revision.TradePlanRevisionId },
                revisionRecordedAt = candidate.Revision.SubmittedAt
            }),
            firstFillInterval = new
            {
                endExclusive = firstFill.SourceTimeEndExclusive,
                precision = firstFill.SourceTimestampPrecision,
                start = firstFill.SourceTimeStart
            },
            firstFillRecordKey = new { fill_id = firstFill.FillId }
        }, ContractVersions.JsonOptions);

    private static string DetermineBatchStatus(int numerator, int denominator, int pending, int quarantined)
    {
        if (denominator == 0)
        {
            return "NEEDS_ATTENTION";
        }

        if (pending == 0 && quarantined == 0 && numerator == denominator)
        {
            return "COMPLETE";
        }

        return numerator * 100 > denominator * 98 ? "PARTIAL" : "NEEDS_ATTENTION";
    }

    private static string DedupScope(string workspaceId, string tradingAccountId, string dedupKey) =>
        $"{workspaceId}\u001F{tradingAccountId}\u001F{dedupKey}";

    private static string ProjectionKey(string episodeId, int projectionVersion) =>
        $"{episodeId}\u001F{projectionVersion.ToString(CultureInfo.InvariantCulture)}";

    private static decimal ParseStoredDecimal(string value) =>
        decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static decimal RoundScale18(decimal value) => decimal.Round(value, 18, MidpointRounding.ToEven);

    private static string CanonicalDecimal(decimal value)
    {
        decimal rounded = RoundScale18(value);
        return rounded == 0 ? "0" : rounded.ToString("0.##################", CultureInfo.InvariantCulture);
    }

    private sealed record ParsedImportCsvRow(
        int SourceRowNumber,
        IReadOnlyList<string> RawFields,
        string VenueSymbol,
        string InstrumentId,
        string BaseAsset,
        string Side,
        DateTimeOffset SourceTimeStart,
        DateTimeOffset SourceTimeEndExclusive,
        string SourceTimestampPrecision,
        decimal PriceQuotePerBase,
        decimal ExecutedQtyBase,
        decimal GrossAmountQuote,
        decimal FeeQty,
        string FeeAsset,
        string CanonicalSignatureSha256);

    private sealed record ProjectionBuildResult(
        TradeEpisodeProjectionRecord Projection,
        IReadOnlyList<EpisodeFillAllocationRecord> Allocations,
        IReadOnlyList<AccountingLedgerEntryRecord> LedgerEntries);

    private sealed record PlanProofSnapshot(
        string Status,
        string ReasonCode,
        string? AssociatedPlanId,
        string? AssociatedPlanRevisionId,
        string? FrozenPlanRevisionId,
        string CandidateIdsJson,
        string BasisJson,
        DateTimeOffset ResolvedAt)
    {
        public static PlanProofSnapshot FromProjection(TradeEpisodeProjectionRecord projection) =>
            new(
                projection.PlanProofStatus,
                projection.PlanProofReasonCode,
                projection.AssociatedPlanId,
                projection.AssociatedPlanRevisionId,
                projection.FrozenPlanRevisionId,
                projection.PlanCandidateIdsJson,
                projection.PlanProofBasisJson,
                projection.PlanProofResolvedAt);
    }
}
