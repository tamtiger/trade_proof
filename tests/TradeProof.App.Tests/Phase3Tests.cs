using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase3Tests
{
    private static readonly DateTimeOffset StartAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await ImportConsumerBuildsClosedEpisodeLedgerAndVerifiedProof();
        await ImportConsumerKeepsLongOnlyViolationsAndMissingThirdFeeSafe();
        await PlanProofUsesSourceIntervalForTimestampAmbiguity();
    }

    private static async Task ImportConsumerBuildsClosedEpisodeLedgerAndVerifiedProof()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        string setupRevisionId = bootstrap.SetupPresets.Single().RevisionId;
        TradePlanRevisionRecord revision = Ok(await app.ArmPlanAsync(actor, new ArmPlanRequest(
            bootstrap.TradingAccountId,
            "BTCUSDT",
            setupRevisionId,
            "100",
            "105",
            "95",
            "10",
            4,
            "Plan before imported execution",
            3600,
            "phase3-plan-verified")), "arm verified plan");

        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,1 USDT\n" +
            "2026-08-27 09:02:00,BTCUSDT,SELL,111,0.4,44.4,0.004 BTC\n" +
            "2026-08-27 09:03:00,BTCUSDT,SELL,121,0.6,72.6,0.5 USDT\n",
            "closed");

        ImportBatchRecord processed = Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "process-closed")), "process closed import");
        Equal("COMPLETE", processed.Status, "all rows reconciled");
        Equal(3, processed.DataRows, "batch data rows");
        Equal(3, processed.ReconciledRows, "batch reconciled rows");
        Equal(0, processed.DuplicateRows, "batch duplicate rows");
        Equal(0, processed.AccountingPendingRows, "batch pending rows");
        Equal(0, processed.QuarantinedRows, "batch quarantined rows");
        Equal(3, app.ImportRowsCount, "one durable import row per data row");
        Equal(3, app.NormalizedFills.Count, "one immutable fill per unique row");
        Equal(3, app.FeeConversions.Count, "one fee conversion per fill");
        Equal(6, app.AccountingLedgerEntries.Count, "two ledger entries per allocation");

        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();
        Equal("CLOSED", projection.State, "episode closes at final sell");
        Equal("0", projection.PositionQtyBase, "closed position quantity");
        Equal("0", projection.OpenCostBasisQuote, "closed cost basis");
        Equal("16", projection.GrossRealizedPnlQuote, "gross realized pnl");
        Equal("1.944", projection.KnownFeeQuote, "known fee quote");
        Equal("14.056", projection.NetRealizedPnlQuote, "net realized pnl");
        Equal("COMPLETE", projection.AccountingQuality, "accounting quality");
        Equal("VERIFIED", projection.PlanProofStatus, "plan proof status");
        Equal("VERIFIED_BEFORE_INTERVAL", projection.PlanProofReasonCode, "plan proof reason");
        Equal(revision.TradePlanRevisionId, projection.FrozenPlanRevisionId, "verified proof freezes revision");
        Equal("1.4056", projection.RMultiple, "R multiple uses frozen planned risk");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.Import && j.State == "COMPLETED"), "IMPORT job is terminal");

        ImportBatchRecord replay = Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "process-closed-replay")), "process replay");
        Equal(processed.ImportBatchId, replay.ImportBatchId, "process replay returns same batch");
        Equal(3, app.NormalizedFills.Count, "process replay does not duplicate fills");
        Equal(1, app.TradeEpisodeProjections.Count, "process replay does not duplicate episode projection");
    }

    private static async Task ImportConsumerKeepsLongOnlyViolationsAndMissingThirdFeeSafe()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,SELL,101,1,101,0.5 USDT\n" +
            "2026-08-27 09:02:00,ETHUSDT,BUY,2500,0.2,500,0.0002 BNB\n",
            "pending");

        ImportBatchRecord processed = Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "process-pending")), "process pending import");
        Equal("NEEDS_ATTENTION", processed.Status, "unsafe rows need attention");
        Equal(2, processed.DataRows, "batch data rows");
        Equal(0, processed.ReconciledRows, "no reconciled rows");
        Equal(1, processed.AccountingPendingRows, "third fee row is pending");
        Equal(1, processed.QuarantinedRows, "sell without position is quarantined");
        Require(app.ImportRows.Any(r => r.Status == "QUARANTINED" && r.SafeErrorCode == "SELL_WITHOUT_OPEN_POSITION"), "long-only violation is safely quarantined");
        Require(app.ImportRows.Any(r => r.Status == "ACCOUNTING_PENDING" && r.SafeErrorCode == "FEE_CONVERSION_MISSING"), "missing third fee is safely pending");

        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();
        Equal("OPEN", projection.State, "pending third-fee buy still opens an episode");
        Equal("FEE_CONVERSION_MISSING", projection.AccountingQuality, "episode quality records missing conversion");
        Require(projection.NetRealizedPnlQuote is null, "net pnl is null when fee conversion is missing");
    }

    private static async Task PlanProofUsesSourceIntervalForTimestampAmbiguity()
    {
        DateTimeOffset ambiguousArmTime = new DateTimeOffset(2026, 8, 27, 9, 1, 0, TimeSpan.Zero).AddMilliseconds(500);
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace(ambiguousArmTime);
        string setupRevisionId = bootstrap.SetupPresets.Single().RevisionId;
        TradePlanRevisionRecord revision = Ok(await app.ArmPlanAsync(actor, new ArmPlanRequest(
            bootstrap.TradingAccountId,
            "BTCUSDT",
            setupRevisionId,
            "100",
            "105",
            "95",
            "10",
            3,
            "Plan inside source timestamp precision",
            3600,
            "phase3-plan-ambiguous")), "arm ambiguous plan");

        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,0 USDT\n",
            "ambiguous");

        Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "process-ambiguous")), "process ambiguous import");

        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();
        Equal("AMBIGUOUS", projection.PlanProofStatus, "plan inside source interval is ambiguous");
        Equal("ARM_INSIDE_INTERVAL", projection.PlanProofReasonCode, "ambiguous reason");
        Equal(revision.TradePlanRevisionId, projection.AssociatedPlanRevisionId, "ambiguous proof keeps associated candidate");
        Require(projection.FrozenPlanRevisionId is null, "ambiguous proof never freezes revision");
        Require(projection.RMultiple is null, "ambiguous proof has no R multiple");

        TradePlanHeaderRecord plan = (await app.GetDashboardAsync(actor)).Plans.Single(p => p.TradePlanId == revision.TradePlanId);
        Equal("CONSUMED", plan.State, "auto-associated ambiguous plan is consumed without becoming verified");
    }

    private static async Task<ImportBatchRecord> ConfirmCsv(
        TradeProofApp app,
        ActorContext actor,
        string tradingAccountId,
        string csv,
        string suffix)
    {
        ObjectIngestReservationRecord reservation = Ok(await app.ReserveRawUploadAsync(actor, new ReserveRawUploadRequest(
            tradingAccountId,
            ContractVersions.BinanceSpotTradeHistoryCsv,
            "CSV",
            $"phase3-reserve-{suffix}")), "reserve csv");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            Encoding.UTF8.GetBytes(csv),
            $"phase3-record-{suffix}")), "record csv");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"phase3-transfer-{suffix}")), "transfer csv");
        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"phase3-validate-{suffix}")), "validate csv");
        ImportPreviewRecord preview = validation.Preview ?? throw new InvalidOperationException("expected preview");
        return Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            $"phase3-confirm-{suffix}")), "confirm csv");
    }

    private static async Task<(TradeProofApp App, FixedTradeProofClock Clock, ManagedIdentity Identity, BootstrapResponse Bootstrap, ActorContext Actor)> NewWorkspace(DateTimeOffset? startAt = null)
    {
        FixedTradeProofClock clock = new(startAt ?? StartAt);
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

    private static void Require(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
        }
    }
}
