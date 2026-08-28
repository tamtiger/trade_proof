using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase4Tests
{
    private static readonly DateTimeOffset StartAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await ThirdAssetFeeConversionUsesPointInTimeDirectAndInverseBars();
        await ContextSnapshotsUseAuthoritativeSequencesAndNoLookaheadBars();
    }

    private static async Task ThirdAssetFeeConversionUsesPointInTimeDirectAndInverseBars()
    {
        (TradeProofApp app, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        await SeedMarket(app);
        await RecordBars(app, "BNBUSDT", "1m", [
            Bar("2026-08-27T09:00:00Z", "300", "310"),
            Bar("2026-08-27T09:01:00Z", "999", "1000")
        ], "bnb");
        await RecordBars(app, "USDTTRY", "1m", [
            Bar("2026-08-27T09:00:00Z", "0.04", "0.05")
        ], "try");

        ImportBatchRecord directBatch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,ETHUSDT,BUY,2500,0.2,500,0.0002 BNB\n",
            "direct");
        ImportBatchRecord directProcessed = Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(directBatch.ImportBatchId, "phase4-process-direct")), "process direct");
        Equal("COMPLETE", directProcessed.Status, "direct third fee reconciles when bar exists");
        FeeConversionRecord direct = app.FeeConversions.Single(c => c.FeeAsset == "BNB");
        Equal("DERIVED", direct.Status, "direct fee status");
        Equal("DIRECT_1M_CLOSE", direct.Method, "direct fee method");
        Equal("300", direct.RateQuotePerFeeAsset, "direct fee uses latest eligible closed bar");
        Equal("0.06", direct.FeeValueQuote, "direct fee value");
        Equal(StartAt.AddMinutes(1), direct.AsOfAt, "direct fee as-of uses bar end");
        Require(direct.MarketBarIdsJson?.Contains("revisionId", StringComparison.Ordinal) == true, "direct fee pins market bar ID");
        Require(direct.MarketBarSourceObservationIdsJson?.Contains("sourceObservationId", StringComparison.Ordinal) == true, "direct fee pins observation ID");
        Require(direct.ConversionPathJson?.Contains("\"direction\":\"DIRECT\"", StringComparison.Ordinal) == true, "direct path is persisted");
        Require(direct.ConversionPathJson?.Contains("\"close\":\"300\"", StringComparison.Ordinal) == true, "future bar close is not used");

        (TradeProofApp inverseApp, _, BootstrapResponse inverseBootstrap, ActorContext inverseActor) = await NewWorkspace();
        await inverseApp.PublishMarketConversionCatalogAsync(new PublishMarketConversionCatalogRequest([
            new MarketConversionCatalogInput("USDTTRY", "USDT", "TRY", true)
        ], "phase4-catalog-inverse"));
        await RecordBars(inverseApp, "USDTTRY", "1m", [
            Bar("2026-08-27T09:00:00Z", "0.04", "0.05")
        ], "inverse-only");
        ImportBatchRecord inverseBatch = await ConfirmCsv(inverseApp, inverseActor, inverseBootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,ETHUSDT,BUY,2500,0.2,500,2 TRY\n",
            "inverse");
        ImportBatchRecord inverseProcessed = Ok(await inverseApp.ProcessImportAsync(inverseActor, new ProcessImportRequest(inverseBatch.ImportBatchId, "phase4-process-inverse")), "process inverse");
        Equal("COMPLETE", inverseProcessed.Status, "inverse third fee reconciles when direct path is absent");
        FeeConversionRecord inverse = inverseApp.FeeConversions.Single(c => c.FeeAsset == "TRY");
        Equal("DERIVED", inverse.Status, "inverse fee status");
        Equal("INVERSE_1M_CLOSE", inverse.Method, "inverse fee method");
        Equal("25", inverse.RateQuotePerFeeAsset, "inverse fee rate");
        Equal("50", inverse.FeeValueQuote, "inverse fee value");
        Require(inverse.ConversionPathJson?.Contains("\"direction\":\"INVERSE\"", StringComparison.Ordinal) == true, "inverse path is persisted");
        Require(app.MarketBarRevisions.All(b => !b.MarketBarRevisionId.Contains(bootstrap.WorkspaceId, StringComparison.Ordinal)), "market bars are tenant-free public records");
    }

    private static async Task ContextSnapshotsUseAuthoritativeSequencesAndNoLookaheadBars()
    {
        (TradeProofApp app, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        await SeedMarket(app);
        await RecordBars(app, "BTCUSDT", "1m", [
            Bar("2026-08-27T09:00:00Z", "101", "100"),
            Bar("2026-08-27T09:01:00Z", "501", "500"),
            Bar("2026-08-27T09:02:00Z", "121", "120")
        ], "btc-1m");
        await RecordBars(app, "BTCUSDT", "5m", [
            Bar("2026-08-27T08:55:00Z", "100", "99")
        ], "btc-5m");

        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,0 USDT\n" +
            "2026-08-27 09:02:00,BTCUSDT,SELL,111,0.4,44.4,0 USDT\n" +
            "2026-08-27 09:03:00,BTCUSDT,SELL,121,0.6,72.6,0 USDT\n",
            "context");
        Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "phase4-process-context")), "process context import");
        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();

        ContextEpisodeTriggerRecord entryTrigger = app.ContextEpisodeTriggers.Single(t => t.EpisodeId == projection.EpisodeId && t.ProjectionVersion == projection.ProjectionVersion && t.Phase == "ENTRY");
        Equal(1, entryTrigger.SourceEventSequence, "ENTRY uses first allocation sequence");
        ContextEpisodeTriggerRecord exitTrigger = app.ContextEpisodeTriggers.Single(t => t.EpisodeId == projection.EpisodeId && t.ProjectionVersion == projection.ProjectionVersion && t.Phase == "EXIT");
        Equal(3, exitTrigger.SourceEventSequence, "EXIT uses final allocation sequence");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.Context && j.SubjectKeyJson.Contains(projection.EpisodeId, StringComparison.Ordinal)), "episode publication enqueues CONTEXT jobs");
        Require(app.Jobs.Where(j => j.WorkType == ContractVersions.Context).All(j => !app.ResolveProvider(j).RequiresExternalLease), "CONTEXT jobs are internal and tenant-fenced");

        IReadOnlyList<ContextSnapshotRecord> snapshots = Ok(await app.ComputeContextSnapshotsAsync(actor, new ComputeContextSnapshotsRequest(projection.EpisodeId, projection.ProjectionVersion, "phase4-compute")), "compute context");
        Equal(4, snapshots.Count, "ENTRY/EXIT for 1m and 5m");
        ContextSnapshotRecord entryOneMinute = snapshots.Single(s => s.Phase == "ENTRY" && s.Timeframe == "1m");
        Equal("101", entryOneMinute.ReferencePrice, "entry snapshot ignores 09:01 future bar");
        Equal(StartAt, entryOneMinute.TargetBarOpenAt, "entry selected bar open");
        Equal(StartAt.AddMinutes(1), entryOneMinute.AsOfAt, "entry as-of");
        Equal("COMPLETE", entryOneMinute.Quality, "entry 1m quality");
        Equal(true, entryOneMinute.AggregationEligible, "complete snapshot is aggregation eligible");
        string frozenInputHash = entryOneMinute.InputHash;

        await RecordBars(app, "BTCUSDT", "1m", [
            Bar("2026-08-27T09:03:00Z", "9000", "9001")
        ], "future");
        IReadOnlyList<ContextSnapshotRecord> replay = Ok(await app.ComputeContextSnapshotsAsync(actor, new ComputeContextSnapshotsRequest(projection.EpisodeId, projection.ProjectionVersion, "phase4-compute-replay")), "compute context replay");
        ContextSnapshotRecord replayEntryOneMinute = replay.Single(s => s.SnapshotId == entryOneMinute.SnapshotId);
        Equal("101", replayEntryOneMinute.ReferencePrice, "future bars do not mutate published snapshot");
        Equal(frozenInputHash, replayEntryOneMinute.InputHash, "published input hash is stable");

        ContextAlgorithmReleaseRecord release = app.ContextAlgorithmReleases.Single();
        Equal(ContractVersions.ContextAlgorithm, release.AlgorithmVersion, "algorithm release version");
        Equal(ContractVersions.ContextParameterSet, release.ParameterSetId, "parameter set");
        ManualContextRecomputeRequestRecord manual = Ok(await app.RequestManualContextRecomputeAsync(actor, new RequestManualContextRecomputeRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "ENTRY",
            "1m",
            1,
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            "phase4-manual")), "manual request");
        Equal(entryTrigger.EventFillId, manual.EventFillId, "manual request resolves authoritative event");
        ManualContextRecomputeRequestRecord manualReplay = Ok(await app.RequestManualContextRecomputeAsync(actor, new RequestManualContextRecomputeRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "ENTRY",
            "1m",
            1,
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            "phase4-manual")), "manual replay");
        Equal(manual.ManualContextRecomputeRequestId, manualReplay.ManualContextRecomputeRequestId, "manual retry is idempotent");
        Require(!(await app.RequestManualContextRecomputeAsync(actor, new RequestManualContextRecomputeRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "ENTRY",
            "5m",
            1,
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            "phase4-manual"))).Succeeded, "manual changed payload conflicts");
        Require(!(await app.RequestManualContextRecomputeAsync(actor, new RequestManualContextRecomputeRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "EXIT",
            "1m",
            1,
            ContractVersions.ContextAlgorithm,
            ContractVersions.ContextParameterSet,
            "phase4-wrong-sequence"))).Succeeded, "EXIT rejects non-final sequence");
        Require(!(await app.RequestManualContextRecomputeAsync(actor, new RequestManualContextRecomputeRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "ENTRY",
            "1m",
            1,
            "mce-wrong",
            ContractVersions.ContextParameterSet,
            "phase4-wrong-release"))).Succeeded, "wrong release is rejected");
    }

    private static async Task SeedMarket(TradeProofApp app)
    {
        Ok(await app.PublishMarketConversionCatalogAsync(new PublishMarketConversionCatalogRequest([
            new MarketConversionCatalogInput("BNBUSDT", "BNB", "USDT", true),
            new MarketConversionCatalogInput("USDTTRY", "USDT", "TRY", true),
            new MarketConversionCatalogInput("BTCUSDT", "BTC", "USDT", true)
        ], "phase4-catalog")), "publish catalog");
    }

    private static async Task RecordBars(TradeProofApp app, string symbol, string timeframe, IReadOnlyList<MarketBarInput> bars, string suffix)
    {
        Ok(await app.RecordMarketBarsAsync(new RecordMarketBarsRequest(symbol, timeframe, bars, $"phase4-bars-{symbol}-{timeframe}-{suffix}")), "record market bars");
    }

    private static MarketBarInput Bar(string openAt, string close, string volume) =>
        new(DateTimeOffset.Parse(openAt), close, volume);

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
            $"phase4-reserve-{suffix}")), "reserve csv");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            Encoding.UTF8.GetBytes(csv),
            $"phase4-record-{suffix}")), "record csv");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"phase4-transfer-{suffix}")), "transfer csv");
        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"phase4-validate-{suffix}")), "validate csv");
        ImportPreviewRecord preview = validation.Preview ?? throw new InvalidOperationException("expected preview");
        return Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            $"phase4-confirm-{suffix}")), "confirm csv");
    }

    private static async Task<(TradeProofApp App, FixedTradeProofClock Clock, BootstrapResponse Bootstrap, ActorContext Actor)> NewWorkspace()
    {
        FixedTradeProofClock clock = new(StartAt);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"local-owner-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        return (app, clock, bootstrap, app.ActorFromBootstrap(bootstrap, identity));
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
