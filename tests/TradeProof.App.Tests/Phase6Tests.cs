using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase6Tests
{
    private static readonly DateTimeOffset ClockStart = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CohortStart = new(2026, 8, 23, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CohortEnd = new(2026, 8, 30, 17, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await WeeklyLabExperimentAndCompletionAreImmutable();
        await AnalyticsExportExpiryAndDeletionAreReferenceClosed();
    }

    private static async Task WeeklyLabExperimentAndCompletionAreImmutable()
    {
        (TradeProofApp app, LabSetup setup) = await BuildReviewedWorkspace("lab");

        WeeklyLabPublicationResult publication = Ok(await app.PublishWeeklyLabAsync(setup.Actor, new PublishWeeklyLabRequest(
            CohortStart,
            CohortEnd,
            "phase6-lab-publish")), "publish lab");
        Equal("weekly_lab_v1", publication.Cohort.WeeklyLabSchemaVersion, "cohort schema");
        Equal("REGULAR", publication.Cohort.CohortType, "cohort type");
        Equal("LOCKED", publication.Cohort.State, "cohort state");
        Equal("Asia/Ho_Chi_Minh", publication.Cohort.WorkspaceTimezone, "workspace timezone");
        Equal("2026-08-24T00:00:00", publication.Cohort.CohortStartLocal, "local start");
        Equal("2026-08-31T00:00:00", publication.Cohort.CohortEndLocalExclusive, "local end");
        Equal("weekly_lab_v1", publication.InputRevision.WeeklyLabSchemaVersion, "input schema");
        Equal("INITIAL_LOCK", publication.InputRevision.Reason, "input reason");
        Require(publication.InputRevision.EpisodeProjectionRefsJson.Contains(setup.Projection.EpisodeId, StringComparison.Ordinal), "input pins episode refs");
        Require(publication.InputRevision.ReviewRevisionRefsJson.Contains(setup.Review.Revision.ReviewRevisionId, StringComparison.Ordinal), "input pins review refs");
        Require(publication.InputRevision.ContextRefMatrixJson.Contains("context_snapshot_id", StringComparison.Ordinal), "input pins context refs");
        Require(publication.ReportRevision.MetricSnapshotIdsJson.Contains(setup.MetricSnapshots[0].MetricSnapshotId, StringComparison.Ordinal), "report pins metric refs");
        Equal("weekly_lab_renderer_v1", publication.ReportRevision.RendererVersion, "renderer version");
        Require(publication.ReportRevision.RenderedSectionsJson.Contains("review_coverage_rate", StringComparison.Ordinal), "renderer references metrics");
        Require(publication.ReportRevision.ContentSha256.Length == 64, "report content hash");

        WeeklyLabPublicationResult replay = Ok(await app.PublishWeeklyLabAsync(setup.Actor, new PublishWeeklyLabRequest(
            CohortStart,
            CohortEnd,
            "phase6-lab-publish")), "publish lab replay");
        Equal(publication.ReportRevision.WeeklyReportRevisionId, replay.ReportRevision.WeeklyReportRevisionId, "lab publish is idempotent");

        BehavioralExperimentRevisionRecord proposed = Ok(await app.ProposeBehavioralExperimentAsync(setup.Actor, new ProposeBehavioralExperimentRequest(
            publication.ReportRevision.WeeklyReportRevisionId,
            "WAIT_FOR_CLOSE",
            "Wait for candle close before entry.",
            "phase6-exp-propose")), "propose experiment");
        Equal("behavioral_experiment_v1", proposed.TaxonomyVersion, "experiment taxonomy");
        Equal("PROPOSED", proposed.State, "experiment proposed");
        Equal(publication.NextCohort.WeeklyCohortId, proposed.TargetWeeklyCohortId, "experiment targets next cohort");

        BehavioralExperimentRevisionRecord confirmed = Ok(await app.ConfirmBehavioralExperimentAsync(setup.Actor, new ConfirmBehavioralExperimentRequest(
            proposed.BehavioralExperimentId,
            proposed.RevisionNo,
            "phase6-exp-confirm")), "confirm experiment");
        Equal("CONFIRMED", confirmed.State, "experiment confirmed");
        Equal(2, confirmed.RevisionNo, "confirm appends revision");
        Require(!(await app.ProposeBehavioralExperimentAsync(setup.Actor, new ProposeBehavioralExperimentRequest(
            publication.ReportRevision.WeeklyReportRevisionId,
            "REDUCE_SIZE_AFTER_LOSS",
            "Reduce next risk after a losing episode.",
            "phase6-exp-second"))).Succeeded, "second confirmed target is rejected");

        WeeklyReviewCompletionRecord completion = Ok(await app.CompleteWeeklyReviewAsync(setup.Actor, new CompleteWeeklyReviewRequest(
            publication.Cohort.WeeklyCohortId,
            publication.ReportRevision.WeeklyReportRevisionId,
            confirmed.BehavioralExperimentRevisionId,
            "phase6-weekly-complete")), "complete weekly review");
        Equal("weekly_review_completion_v1", completion.SchemaVersion, "completion schema");
        Equal(publication.Cohort.WeeklyCohortId, completion.WeeklyCohortId, "completion cohort");
        Equal(confirmed.BehavioralExperimentRevisionId, completion.BehavioralExperimentRevisionId, "completion experiment");
    }

    private static async Task AnalyticsExportExpiryAndDeletionAreReferenceClosed()
    {
        (TradeProofApp app, LabSetup setup) = await BuildReviewedWorkspace("rights");
        WeeklyLabPublicationResult publication = Ok(await app.PublishWeeklyLabAsync(setup.Actor, new PublishWeeklyLabRequest(
            CohortStart,
            CohortEnd,
            "phase6-rights-lab")), "publish rights lab");

        ProductAnalyticsEventRecord analytics = Ok(await app.RecordProductAnalyticsEventAsync(setup.Actor, new RecordProductAnalyticsEventRequest(
            "weekly_lab_opened",
            "WeeklyReportRevision",
            publication.ReportRevision.WeeklyReportRevisionId,
            ClockStart.AddHours(3),
            "phase6-analytics-event")), "record analytics");
        Equal("product_analytics_event_v1", analytics.SchemaVersion, "analytics schema");
        Require(!analytics.PayloadJson.Contains("BTCUSDT", StringComparison.OrdinalIgnoreCase), "analytics omits business content");

        IReadOnlyList<WorkspaceProductMetricSnapshotRecord> productMetrics = Ok(await app.PublishWorkspaceProductMetricsAsync(setup.Actor, new PublishWorkspaceProductMetricsRequest(
            CohortStart,
            CohortEnd,
            "phase6-product-metrics")), "publish product metrics");
        WorkspaceProductMetricSnapshotRecord opened = productMetrics.Single(m => m.MetricId == "weekly_lab_opened_count");
        Equal("workspace_product_metric_snapshot_v1", opened.SchemaVersion, "workspace product metric schema");
        Equal(1, opened.ValueInteger, "opened count");

        InternalAggregateProductMetricSnapshotRecord aggregate = Ok(await app.PublishInternalAggregateProductMetricAsync(new PublishInternalAggregateProductMetricRequest(
            [setup.Actor.WorkspaceId],
            "weekly_lab_opened_count",
            CohortStart,
            CohortEnd,
            "phase6-internal-aggregate")), "publish internal aggregate");
        Equal("internal_aggregate_product_metric_snapshot_v1", aggregate.SchemaVersion, "internal aggregate schema");
        Equal("PRIVACY_THRESHOLD", aggregate.NullReason, "privacy threshold");

        ExternalAnalyticsProjectionRecord projection = Ok(await app.ProjectExternalAnalyticsAsync(setup.Actor, new ProjectExternalAnalyticsRequest(
            analytics.ProductAnalyticsEventId,
            "phase6-external-project")), "external analytics projection");
        Equal("product_analytics_external_v1", projection.SchemaVersion, "external projection schema");
        Require(projection.PayloadJson.Contains("weekly_lab_opened", StringComparison.Ordinal), "external payload keeps allowed event type");
        Require(!projection.PayloadJson.Contains(publication.ReportRevision.WeeklyReportRevisionId, StringComparison.Ordinal), "external payload omits internal IDs");

        ExternalAnalyticsPurgeRecord purge = Ok(await app.PurgeExternalAnalyticsAsync(setup.Actor, new PurgeExternalAnalyticsRequest(
            projection.ExternalAnalyticsProjectionId,
            "phase6-external-purge")), "purge external analytics");
        Equal("ANALYTICS_PURGE", purge.WorkType, "analytics purge work type");
        Equal("ABSENCE_VERIFIED", purge.State, "analytics purge state");

        TradeProofExportRecord export = Ok(await app.RequestTradeProofExportAsync(setup.Actor, new RequestTradeProofExportRequest(
            publication.ReportRevision.WeeklyReportRevisionId,
            CohortEnd,
            "phase6-export")), "request export");
        Equal("tradeproof_export_v1", export.ExportSchemaVersion, "export schema");
        Equal("READY", export.State, "export state");
        Equal("STANDARD", export.ServiceClass, "export service class");
        Require(export.ManifestJson.Contains("tradeproof_export_manifest_v1", StringComparison.Ordinal), "export manifest schema");
        Require(export.ManifestJson.Contains("weekly_lab_export_projection_v1", StringComparison.Ordinal), "export includes lab projection");
        Require(export.CsvEntriesJson.Contains("spreadsheet_escape_v1", StringComparison.Ordinal), "csv entries record spreadsheet escaping");
        Require(!export.CsvEntriesJson.Contains("=BTCUSDT", StringComparison.Ordinal), "csv entries escape spreadsheet formula prefixes");

        ExportRoundTripValidationRecord roundTrip = Ok(await app.ValidateExportRoundTripAsync(setup.Actor, new ValidateExportRoundTripRequest(
            export.TradeProofExportId,
            "phase6-round-trip")), "round trip export");
        Equal("tradeproof_export_round_trip_v1", roundTrip.ReaderProfileVersion, "round trip reader");
        Require(roundTrip.Passed, "round trip passes");

        ExportExpiryRecord expiry = Ok(await app.ExpireExportAsync(setup.Actor, new ExpireExportRequest(
            export.TradeProofExportId,
            "phase6-export-expiry")), "expire export");
        Equal("EXPORT_EXPIRY", expiry.WorkType, "export expiry work type");
        Equal("ABSENCE_VERIFIED", expiry.State, "export expiry state");
        Equal("EXPIRED", app.TradeProofExports.Single(e => e.TradeProofExportId == export.TradeProofExportId).State, "export state after expiry");

        WorkspaceDeletionResult deletion = Ok(await app.RequestWorkspaceDeletionAsync(setup.Actor, new RequestWorkspaceDeletionRequest(
            "phase6-delete-request")), "request deletion");
        Equal("workspace_deletion_v1", deletion.Deletion.SchemaVersion, "deletion schema");
        Equal("FENCED", deletion.Deletion.State, "deletion state");
        Equal(2, deletion.Deletion.GuardGeneration, "deletion generation increments");
        Require(deletion.Targets.Any(t => t.TargetType == "PRIMARY_TENANT_DATA"), "primary target is present");
        Require(deletion.Targets.Any(t => t.TargetType == "EXPORT_ARCHIVES"), "export target is present");
        Require(deletion.CancelledTenantWorkCount > 0, "deletion drains queued work");
        Require(deletion.RevokedExportCount > 0, "deletion revokes export archives");

        WorkspaceDeletionResult completed = Ok(await app.CompleteWorkspaceDeletionAsync(setup.Actor, new CompleteWorkspaceDeletionRequest(
            deletion.Deletion.WorkspaceDeletionId,
            "phase6-delete-complete")), "complete deletion");
        Equal("DELETED", completed.Deletion.State, "workspace deletion completed");
        Require(app.WorkspaceDeletionTombstones.Any(t => t.WorkspaceDeletionId == completed.Deletion.WorkspaceDeletionId), "deletion tombstone exists");
    }

    private static async Task<(TradeProofApp App, LabSetup Setup)> BuildReviewedWorkspace(string suffix)
    {
        FixedTradeProofClock clock = new(ClockStart);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"phase6-{suffix}-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        ActorContext actor = app.ActorFromBootstrap(bootstrap, identity);
        SetupPresetRevisionRecord setup = Ok(await app.CreateSetupPresetAsync(actor, new CreateSetupPresetRequest(
            "Phase 6 setup",
            [new ChecklistItemInput("Wait close", true)],
            $"phase6-setup-{suffix}")), "create setup");
        await SeedMarketData(app);
        await ArmPlan(app, actor, bootstrap.TradingAccountId, setup.RevisionId, "BTCUSDT", suffix);
        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,0 USDT\n" +
            "2026-08-27 09:02:00,BTCUSDT,SELL,111,1,111,0 USDT\n",
            suffix);
        Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, $"phase6-process-{suffix}")), "process import");
        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();
        Ok(await app.ComputeContextSnapshotsAsync(actor, new ComputeContextSnapshotsRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            $"phase6-context-{suffix}")), "compute context");
        string requiredItemId = setup.Checklist.Single(i => i.Required).ChecklistItemId;
        EpisodeReviewResult review = Ok(await app.CompleteEpisodeReviewAsync(actor, new CompleteEpisodeReviewRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "TARGET_REACHED",
            null,
            false,
            [],
            null,
            false,
            false,
            new Dictionary<string, bool> { [requiredItemId] = true },
            "FOCUSED",
            "Clean weekly review input.",
            null,
            $"phase6-review-{suffix}")), "complete review");
        IReadOnlyList<MetricSnapshotRecord> metrics = Ok(await app.PublishMetricSnapshotsAsync(actor, new PublishMetricSnapshotsRequest(
            CohortStart,
            CohortEnd,
            $"phase6-metrics-{suffix}")), "publish metrics");
        return (app, new LabSetup(clock, bootstrap, actor, projection, review, metrics));
    }

    private static async Task SeedMarketData(TradeProofApp app)
    {
        Ok(await app.PublishMarketConversionCatalogAsync(new PublishMarketConversionCatalogRequest(
            [new MarketConversionCatalogInput("BTCUSDT", "BTC", "USDT", true)],
            "phase6-catalog")), "publish market catalog");
        Ok(await app.RecordMarketBarsAsync(new RecordMarketBarsRequest(
            "BTCUSDT",
            "1m",
            [
                new MarketBarInput(ClockStart.AddMinutes(-2), "101", "100"),
                new MarketBarInput(ClockStart.AddMinutes(-1), "111", "120"),
                new MarketBarInput(ClockStart, "121", "140")
            ],
            "phase6-bars-1m")), "record 1m bars");
        Ok(await app.RecordMarketBarsAsync(new RecordMarketBarsRequest(
            "BTCUSDT",
            "5m",
            [new MarketBarInput(ClockStart.AddMinutes(-5), "100", "90")],
            "phase6-bars-5m")), "record 5m bars");
    }

    private static async Task<TradePlanRevisionRecord> ArmPlan(TradeProofApp app, ActorContext actor, string tradingAccountId, string setupRevisionId, string symbol, string suffix) =>
        Ok(await app.ArmPlanAsync(actor, new ArmPlanRequest(
            tradingAccountId,
            symbol,
            setupRevisionId,
            "100",
            "105",
            "95",
            "10",
            4,
            "Phase 6 test plan",
            7200,
            $"phase6-plan-{suffix}")), "arm plan");

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
            $"phase6-reserve-{suffix}")), "reserve csv");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            Encoding.UTF8.GetBytes(csv),
            $"phase6-record-{suffix}")), "record csv");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"phase6-transfer-{suffix}")), "transfer csv");
        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"phase6-validate-{suffix}")), "validate csv");
        ImportPreviewRecord preview = validation.Preview ?? throw new InvalidOperationException("expected preview");
        return Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            $"phase6-confirm-{suffix}")), "confirm csv");
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

    private sealed record LabSetup(
        FixedTradeProofClock Clock,
        BootstrapResponse Bootstrap,
        ActorContext Actor,
        TradeEpisodeProjectionRecord Projection,
        EpisodeReviewResult Review,
        IReadOnlyList<MetricSnapshotRecord> MetricSnapshots);
}
