using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase2Tests
{
    private static readonly DateTimeOffset StartAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await ReservationCreatesSingleUseCapabilityAndFinalizeChain();
        await TransferValidatesCsvPreviewAndPurgeChains();
        await CsvValidationRejectsUnsafeFilesBeforeBusinessWrites();
        await ConfirmImportIsIdempotentHashBoundAndZeroRows();
        await StagedFillFoundationUsesSafeFingerprintAndDispositions();
    }

    private static async Task ReservationCreatesSingleUseCapabilityAndFinalizeChain()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();

        ObjectIngestReservationRecord reservation = Ok(await app.ReserveRawUploadAsync(actor, new ReserveRawUploadRequest(
            bootstrap.TradingAccountId,
            ContractVersions.BinanceSpotTradeHistoryCsv,
            "CSV",
            "reserve-1")), "reserve raw upload");

        Equal("RAW_UPLOAD", reservation.Purpose, "raw upload purpose");
        Equal("RESERVED", reservation.State, "reservation state");
        Equal(1, reservation.LeaseGeneration, "lease generation");
        Equal(StartAt.AddMinutes(15), reservation.WriteExpiresAt, "write capability expires after 15 minutes");
        Equal(StartAt.AddHours(1), reservation.AbsenceDueAt, "absence due after 1 hour");
        Equal(bootstrap.TradingAccountId, reservation.TradingAccountId, "reservation binds trading account");

        TenantControlJobRecord finalizer = app.Jobs.Single(j => j.WorkType == ContractVersions.ObjectIngestFinalize);
        Require(finalizer.SubjectKeyJson.Contains(reservation.ObjectIngestReservationId, StringComparison.Ordinal), "finalizer subject binds reservation");
        ProviderDispatchPlan provider = app.ResolveProvider(finalizer);
        Require(provider.RequiresExternalLease, "object ingest finalizer needs external lease");
        Equal("local:object-ingest-finalize", provider.ProviderLookupKey, "object finalizer provider key");

        ProviderWriteResult firstWrite = Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            ValidCsvBytes(),
            "record-1")), "record bytes");
        Equal(Encoding.UTF8.GetByteCount(ValidCsvText()), firstWrite.SizeBytes, "recorded byte count");
        Equal(StartAt, firstWrite.RecordedAt, "recorded at trusted server time");

        CommandResult<ProviderWriteResult> secondWrite = await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            ValidCsvBytes(),
            "record-2"));
        Equal("WRITE_CAPABILITY_ALREADY_CONSUMED", Fail(secondWrite, "second provider write"), "capability is single-use");
    }

    private static async Task TransferValidatesCsvPreviewAndPurgeChains()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        ObjectIngestReservationRecord reservation = await ReserveAndWriteValidCsv(app, actor, bootstrap.TradingAccountId);

        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            "transfer-1")), "transfer raw upload");
        Equal(reservation.ReservedUploadId, transfer.Upload.UploadId, "transfer uses preallocated upload");
        Equal("QUARANTINED", transfer.Upload.State, "upload is quarantined after RECEIVE");
        Equal(StartAt.AddHours(20), transfer.Upload.ForcedPurgeAt, "forced purge deadline");
        Equal(StartAt.AddHours(24), transfer.Upload.PurgeDueAt, "purge due deadline");
        Require(app.UploadObjectLeases.Any(l => l.UploadId == transfer.Upload.UploadId && l.State == "ACTIVE"), "transfer creates active object lease");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.UploadValidate && j.SubjectKeyJson.Contains(transfer.Upload.UploadId, StringComparison.Ordinal)), "transfer enqueues UPLOAD_VALIDATE");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.UploadPurge && j.SubjectKeyJson.Contains(transfer.Upload.UploadId, StringComparison.Ordinal)), "transfer enqueues UPLOAD_PURGE");

        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            "validate-1")), "validate upload");
        Equal("ACCEPTED", validation.Upload.State, "valid CSV is accepted");
        Require(validation.Preview is not null, "valid CSV creates preview");
        Equal(ContractVersions.ImportPreview, validation.Preview!.SchemaVersion, "preview schema");
        Equal(2, validation.Preview.DataRows, "preview counts data rows");
        Equal("READY", validation.Preview.State, "preview is ready");
        Require(validation.Preview.Symbols.SequenceEqual(["BTCUSDT", "ETHUSDT"]), "preview symbols are sanitized and sorted");
        Require(app.ImportBatches.Count == 0, "preview creates zero import batches");
        Require(app.ImportRowsCount == 0, "preview creates zero import rows");
        Require(app.StagedFills.Count == 0, "preview creates zero staged fills");

        UploadValidationResponse replay = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            "validate-2")), "validate replay");
        Equal(validation.Preview.ImportPreviewId, replay.Preview!.ImportPreviewId, "validate retry returns same preview");

        UploadPurgeResponse purge = Ok(await app.PurgeUploadAsync(actor, new PurgeUploadRequest(
            transfer.Upload.UploadId,
            "purge-1")), "purge upload");
        Equal("PURGED", purge.Upload.State, "purge state");
        RequireFalse(app.HasProviderBytesForTest(transfer.Upload.UploadId), "purge removes raw provider bytes");
        Require(app.UploadAbsenceVerifications.Any(v => v.UploadId == transfer.Upload.UploadId), "purge records absence verification");
    }

    private static async Task CsvValidationRejectsUnsafeFilesBeforeBusinessWrites()
    {
        await RejectsInvalidCsv("Date(UTC),Pair,Side,Price,Executed,Amount\n2026-08-27 09:00:00,BTCUSDT,BUY,100,0.1,10\n", "HEADER_MISMATCH");
        await RejectsInvalidBytes([0xC3, 0x28], "UTF8_INVALID");
        await RejectsInvalidBytes(new byte[20 * 1024 * 1024 + 1], "UPLOAD_TOO_LARGE");
        await RejectsInvalidCsv(BuildRows(100_001), "CSV_ROW_LIMIT_EXCEEDED");
    }

    private static async Task ConfirmImportIsIdempotentHashBoundAndZeroRows()
    {
        (TradeProofApp app, FixedTradeProofClock clock, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        ImportPreviewRecord preview = await CreateReadyPreview(app, actor, bootstrap.TradingAccountId);

        ImportBatchRecord batch = Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            "confirm-1")), "confirm import");
        Equal(preview.ImportPreviewId, batch.SourceImportPreviewId, "batch copies preview id");
        Equal(preview.PreviewSummarySha256, batch.SourcePreviewSummarySha256, "batch copies preview hash");
        Equal(ContractVersions.ImportPreview, batch.SourcePreviewSchemaVersion, "batch copies preview schema");
        Equal("UPLOADED", batch.Status, "batch starts uploaded");
        Equal(0, app.ImportRowsCount, "confirm command creates zero rows");
        Equal(0, app.StagedFills.Count, "confirm command creates zero staged fills");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.Import && j.SubjectKeyJson.Contains(batch.ImportBatchId, StringComparison.Ordinal)), "confirm enqueues IMPORT chain");

        ImportBatchRecord replay = Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            "confirm-1")), "confirm replay");
        Equal(batch.ImportBatchId, replay.ImportBatchId, "exact confirm retry returns same batch");

        CommandResult<ImportBatchRecord> changedRetry = await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            "0".PadLeft(64, '0'),
            "confirm-1"));
        Equal("IDEMPOTENCY_CONFLICT", Fail(changedRetry, "changed confirm retry"), "changed confirm retry conflicts");

        ImportBatchRecord existingForNewKey = Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            "confirm-2")), "confirm with new key");
        Equal(batch.ImportBatchId, existingForNewKey.ImportBatchId, "confirmed preview returns existing batch for a new key");

        ImportPreviewRecord expiringPreview = await CreateReadyPreview(app, actor, bootstrap.TradingAccountId, "late");
        clock.Advance(TimeSpan.FromMinutes(30));
        CommandResult<ImportBatchRecord> expired = await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            expiringPreview.ImportPreviewId,
            expiringPreview.PreviewSummarySha256,
            "confirm-expired"));
        Equal("IMPORT_PREVIEW_EXPIRED", Fail(expired, "expired preview confirm"), "confirm requires trusted time before expires_at");
    }

    private static async Task StagedFillFoundationUsesSafeFingerprintAndDispositions()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        ImportPreviewRecord preview = await CreateReadyPreview(app, actor, bootstrap.TradingAccountId);
        ImportBatchRecord batch = Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            "confirm-staged")), "confirm for staged shell");

        StagedFillRecord staged = app.CreateStagedFillCandidateForTest(actor, new CreateStagedFillCandidateRequest(
            batch.ImportBatchId,
            2,
            "ETHUSDT",
            "BUY",
            "2026-08-27T09:02:00Z",
            "2500.00",
            "0.20",
            "500.00",
            "0.0002",
            "ETH"));

        StagedFillRecord duplicateInput = app.CreateStagedFillCandidateForTest(actor, new CreateStagedFillCandidateRequest(
            batch.ImportBatchId,
            2,
            "ETHUSDT",
            "BUY",
            "2026-08-27T09:02:00Z",
            "2500.00",
            "0.20",
            "500.00",
            "0.0002",
            "ETH"));

        Equal(staged.SourceRowFingerprintSha256, duplicateInput.SourceRowFingerprintSha256, "source row fingerprint is stable");
        Equal(ContractVersions.StagedFill, staged.StagedFillSchemaVersion, "staged fill schema");
        Require(app.AllowedImportRowDispositions.SequenceEqual(["RECONCILED", "DUPLICATE", "ACCOUNTING_PENDING", "QUARANTINED"]), "progress exposes four row dispositions");

        StagedFillDispositionRecord disposition = app.ResolveStagedFillForTest(actor, staged.StagedFillId, "DISCARDED_AS_DUPLICATE", "nf_existing_1");
        Equal("DISCARDED_AS_DUPLICATE", disposition.Outcome, "disposition outcome");
        Equal(staged.StagedFillId, disposition.StagedFillId, "disposition binds staged fill");

        ImportProgressResponse progress = Ok(await app.GetImportProgressAsync(actor, batch.ImportBatchId), "progress");
        Equal(batch.ImportBatchId, progress.BatchId, "progress batch id");
        Equal(0, progress.ReconciledRows, "progress uses safe zero counters before Week 3 consumer");
        string[] safeErrorPropertyNames = typeof(SafeRowErrorRecord).GetProperties().Select(p => p.Name).ToArray();
        Require(!safeErrorPropertyNames.Any(p =>
            p.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Filename", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Path", StringComparison.OrdinalIgnoreCase)), "safe errors do not expose raw cells, filenames or paths");
    }

    private static async Task RejectsInvalidCsv(string csv, string expectedCode) =>
        await RejectsInvalidBytes(Encoding.UTF8.GetBytes(csv), expectedCode);

    private static async Task RejectsInvalidBytes(byte[] bytes, string expectedCode)
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        ObjectIngestReservationRecord reservation = Ok(await app.ReserveRawUploadAsync(actor, new ReserveRawUploadRequest(
            bootstrap.TradingAccountId,
            ContractVersions.BinanceSpotTradeHistoryCsv,
            "CSV",
            $"reserve-invalid-{expectedCode}")), "reserve invalid upload");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            bytes,
            $"record-invalid-{expectedCode}")), "record invalid upload");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"transfer-invalid-{expectedCode}")), "transfer invalid upload");

        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"validate-invalid-{expectedCode}")), "validate invalid upload");
        Equal("REJECTED", validation.Upload.State, "invalid upload is rejected");
        Equal(expectedCode, validation.SafeErrorCode, "safe reject code");
        Require(validation.Preview is null, "invalid upload creates no preview");
        Require(app.ImportBatches.Count == 0, "invalid upload creates zero batches");
        Require(app.ImportRowsCount == 0, "invalid upload creates zero import rows");
        Require(app.StagedFills.Count == 0, "invalid upload creates zero staged fills");
    }

    private static async Task<ObjectIngestReservationRecord> ReserveAndWriteValidCsv(TradeProofApp app, ActorContext actor, string tradingAccountId, string suffix = "valid")
    {
        ObjectIngestReservationRecord reservation = Ok(await app.ReserveRawUploadAsync(actor, new ReserveRawUploadRequest(
            tradingAccountId,
            ContractVersions.BinanceSpotTradeHistoryCsv,
            "CSV",
            $"reserve-{suffix}")), "reserve valid upload");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            ValidCsvBytes(),
            $"record-{suffix}")), "record valid upload");
        return reservation;
    }

    private static async Task<ImportPreviewRecord> CreateReadyPreview(TradeProofApp app, ActorContext actor, string tradingAccountId, string suffix = "ready")
    {
        ObjectIngestReservationRecord reservation = await ReserveAndWriteValidCsv(app, actor, tradingAccountId, suffix);
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"transfer-{suffix}")), "transfer valid preview");
        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"validate-{suffix}")), "validate valid preview");
        return validation.Preview ?? throw new InvalidOperationException("expected preview");
    }

    private static byte[] ValidCsvBytes() => Encoding.UTF8.GetBytes(ValidCsvText());

    private static string ValidCsvText() =>
        "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
        "2026-08-27 09:01:00,BTCUSDT,BUY,100.50,0.10,10.05,0.0001 BNB\n" +
        "2026-08-27 09:02:00,ETHUSDT,SELL,2500.00,0.20,500.00,0.0002 ETH\n";

    private static string BuildRows(int count)
    {
        StringBuilder builder = new("Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n");
        for (int i = 0; i < count; i++)
        {
            builder.Append("2026-08-27 09:01:00,BTCUSDT,BUY,100.50,0.10,10.05,0.0001 BNB\n");
        }

        return builder.ToString();
    }

    private static async Task<(TradeProofApp App, FixedTradeProofClock Clock, ManagedIdentity Identity, BootstrapResponse Bootstrap, ActorContext Actor)> NewWorkspace()
    {
        FixedTradeProofClock clock = new(StartAt);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"local-owner-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        return (app, clock, identity, bootstrap, app.ActorFromBootstrap(bootstrap, identity));
    }

    private static T Ok<T>(CommandResult<T> result, string label)
    {
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException($"{label} expected success but got {result.ErrorCode ?? "null value"}.");
        }

        return result.Value;
    }

    private static string Fail<T>(CommandResult<T> result, string label)
    {
        if (result.Succeeded)
        {
            throw new InvalidOperationException($"{label} expected failure.");
        }

        return result.ErrorCode ?? throw new InvalidOperationException($"{label} failed without an error code.");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }

    private static void RequireFalse(bool condition, string label) => Require(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
        }
    }
}
