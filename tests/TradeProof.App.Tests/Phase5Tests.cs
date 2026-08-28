using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase5Tests
{
    private static readonly DateTimeOffset StartAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await ReviewRevisionAndScreenshotLifecycleAreAppendOnly();
        await MetricsAndDashboardExposeQualityAndDrillDown();
    }

    private static async Task ReviewRevisionAndScreenshotLifecycleAreAppendOnly()
    {
        (TradeProofApp app, FixedTradeProofClock clock, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        SetupPresetRevisionRecord setup = Ok(await app.CreateSetupPresetAsync(actor, new CreateSetupPresetRequest(
            "Breakout",
            [
                new ChecklistItemInput("Wait close", true),
                new ChecklistItemInput("Optional note", false)
            ],
            "phase5-setup")), "create setup");
        await ArmPlan(app, actor, bootstrap.TradingAccountId, setup.RevisionId, "BTCUSDT", "phase5-plan");
        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,0 USDT\n" +
            "2026-08-27 09:02:00,BTCUSDT,SELL,111,1,111,0 USDT\n",
            "review");
        Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "phase5-process-review")), "process review import");
        TradeEpisodeProjectionRecord projection = app.TradeEpisodeProjections.Single();
        string requiredItemId = setup.Checklist.Single(i => i.Required).ChecklistItemId;

        ObjectIngestReservationRecord attachmentReservation = Ok(await app.ReserveReviewAttachmentAsync(actor, new ReserveReviewAttachmentRequest(
            bootstrap.TradingAccountId,
            "SCREENSHOT",
            "phase5-attachment-reserve")), "reserve screenshot");
        Require(attachmentReservation.ReservedAttachmentId is not null, "screenshot reservation preallocates attachment id");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            attachmentReservation.ObjectIngestReservationId,
            attachmentReservation.WriteCapabilityId,
            PngBytes(),
            "phase5-attachment-record")), "record screenshot bytes");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            attachmentReservation.ObjectIngestReservationId,
            "phase5-attachment-transfer")), "transfer screenshot");
        AttachmentValidationResponse validated = Ok(await app.ValidateAttachmentUploadAsync(actor, new ValidateAttachmentUploadRequest(
            transfer.Upload.UploadId,
            "phase5-attachment-validate")), "validate screenshot");
        AttachmentRecord attachment = validated.Attachment ?? throw new InvalidOperationException("expected active attachment");
        Equal("ACTIVE", attachment.State, "attachment state");
        Equal("PASSED", attachment.ScanStatus, "attachment scan");
        Require(app.Jobs.Any(j => j.WorkType == ContractVersions.AttachmentDelete && j.SubjectKeyJson.Contains(attachment.AttachmentId, StringComparison.Ordinal)), "attachment delete work type is registered");

        EpisodeReviewResult completed = Ok(await app.CompleteEpisodeReviewAsync(actor, new CompleteEpisodeReviewRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "OTHER",
            "Manual scale out",
            true,
            ["CHECKLIST_MISSED", "STOP_MOVED_AWAY"],
            null,
            true,
            false,
            new Dictionary<string, bool> { [requiredItemId] = false },
            "CALM",
            "Missed one required confirmation.",
            attachment.AttachmentId,
            "phase5-review-complete")), "complete review");
        Equal("COMPLETED", completed.Review.State, "review state");
        Equal(1, completed.Revision.RevisionNo, "first review revision");
        Equal(completed.Review.CompletedAt, completed.Revision.RecordedAt, "first revision time");
        Equal("exit_reason_v1", completed.Revision.ExitReasonTaxonomyVersion, "exit taxonomy");
        Equal("breach_type_v1", completed.Revision.BreachTaxonomyVersion, "breach taxonomy");
        Equal("emotion_v1", completed.Revision.EmotionTaxonomyVersion, "emotion taxonomy");
        Require(completed.Revision.ContentSha256.Length == 64, "review content hash is sha256");
        ReviewRevisionAttachmentRecord join = app.ReviewRevisionAttachments.Single(j => j.ReviewRevisionId == completed.Revision.ReviewRevisionId);
        Equal(attachment.AttachmentId, join.AttachmentId, "review revision pins attachment");
        Equal(attachment.ContentSha256, join.AttachmentContentSha256, "join pins attachment hash");

        EpisodeReviewResult replay = Ok(await app.CompleteEpisodeReviewAsync(actor, new CompleteEpisodeReviewRequest(
            projection.EpisodeId,
            projection.ProjectionVersion,
            "OTHER",
            "Manual scale out",
            true,
            ["CHECKLIST_MISSED", "STOP_MOVED_AWAY"],
            null,
            true,
            false,
            new Dictionary<string, bool> { [requiredItemId] = false },
            "CALM",
            "Missed one required confirmation.",
            attachment.AttachmentId,
            "phase5-review-complete")), "complete review replay");
        Equal(completed.Revision.ReviewRevisionId, replay.Revision.ReviewRevisionId, "review completion is idempotent");

        clock.Advance(TimeSpan.FromMinutes(10));
        EpisodeReviewResult revised = Ok(await app.ReviseEpisodeReviewAsync(actor, new ReviseEpisodeReviewRequest(
            completed.Review.ReviewId,
            projection.ProjectionVersion,
            1,
            "TARGET_REACHED",
            null,
            false,
            [],
            null,
            false,
            false,
            new Dictionary<string, bool> { [requiredItemId] = true },
            null,
            "Followed the checklist on edit.",
            null,
            "phase5-review-revise")), "revise review");
        Equal(2, revised.Revision.RevisionNo, "revision increments");
        Equal(completed.Review.CompletedAt, revised.Review.CompletedAt, "completed at is preserved");
        Equal("COMPLETED", revised.Review.State, "revised review is completed");
        Require(revised.Revision.RecordedAt > completed.Revision.RecordedAt, "revision records edit time");
        Require(!(await app.ReviseEpisodeReviewAsync(actor, new ReviseEpisodeReviewRequest(
            completed.Review.ReviewId,
            projection.ProjectionVersion,
            1,
            "TARGET_REACHED",
            null,
            false,
            [],
            null,
            false,
            false,
            new Dictionary<string, bool> { [requiredItemId] = true },
            null,
            null,
            null,
            "phase5-review-stale"))).Succeeded, "stale expected revision is rejected");

        AttachmentDeleteResponse deleted = Ok(await app.DeleteAttachmentAsync(actor, new DeleteAttachmentRequest(
            attachment.AttachmentId,
            "phase5-attachment-delete")), "delete attachment");
        Equal("DELETED", deleted.Attachment.State, "attachment deletion state");
        Require(app.AttachmentTombstones.Any(t => t.AttachmentId == attachment.AttachmentId), "attachment deletion writes tombstone");
        Equal(attachment.AttachmentContentVersionId, join.AttachmentContentVersionId, "historical join keeps content version after delete");
    }

    private static async Task MetricsAndDashboardExposeQualityAndDrillDown()
    {
        (TradeProofApp app, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        SetupPresetRevisionRecord setup = Ok(await app.CreateSetupPresetAsync(actor, new CreateSetupPresetRequest(
            "Momentum",
            [new ChecklistItemInput("Confirm higher low", true)],
            "phase5-metric-setup")), "create metric setup");
        string requiredItemId = setup.Checklist.Single(i => i.Required).ChecklistItemId;

        await ArmPlan(app, actor, bootstrap.TradingAccountId, setup.RevisionId, "BTCUSDT", "metric-btc");
        await ArmPlan(app, actor, bootstrap.TradingAccountId, setup.RevisionId, "ETHUSDT", "metric-eth");
        await ArmPlan(app, actor, bootstrap.TradingAccountId, setup.RevisionId, "SOLUSDT", "metric-sol");
        ImportBatchRecord batch = await ConfirmCsv(app, actor, bootstrap.TradingAccountId,
            "Date(UTC),Pair,Side,Price,Executed,Amount,Fee\n" +
            "2026-08-27 09:01:00,BTCUSDT,BUY,100,1,100,0 USDT\n" +
            "2026-08-27 09:02:00,BTCUSDT,SELL,110,1,110,0 USDT\n" +
            "2026-08-27 09:03:00,ETHUSDT,BUY,200,1,200,0 USDT\n" +
            "2026-08-27 09:04:00,ETHUSDT,SELL,190,1,190,0 USDT\n" +
            "2026-08-27 09:05:00,SOLUSDT,BUY,50,1,50,0 USDT\n" +
            "2026-08-27 09:06:00,SOLUSDT,SELL,55,1,55,0 USDT\n",
            "metrics");
        Ok(await app.ProcessImportAsync(actor, new ProcessImportRequest(batch.ImportBatchId, "phase5-process-metrics")), "process metric import");

        foreach (TradeEpisodeProjectionRecord projection in app.TradeEpisodeProjections.Take(2))
        {
            bool breach = projection.InstrumentId.Contains("ethusdt", StringComparison.Ordinal);
            Ok(await app.CompleteEpisodeReviewAsync(actor, new CompleteEpisodeReviewRequest(
                projection.EpisodeId,
                projection.ProjectionVersion,
                breach ? "STOP_HIT" : "TARGET_REACHED",
                null,
                breach,
                breach ? ["RISK_EXCEEDED"] : [],
                null,
                false,
                breach,
                new Dictionary<string, bool> { [requiredItemId] = true },
                breach ? "FRUSTRATED" : "FOCUSED",
                breach ? "Risk exceeded." : "Clean execution.",
                null,
                $"phase5-review-{projection.EpisodeId}")), "complete metric review");
        }

        IReadOnlyList<MetricSnapshotRecord> snapshots = Ok(await app.PublishMetricSnapshotsAsync(actor, new PublishMetricSnapshotsRequest(
            StartAt,
            StartAt.AddDays(7),
            "phase5-metrics-publish")), "publish metrics");
        MetricSnapshotRecord reviewCoverage = snapshots.Single(s => s.MetricId == "review_coverage_rate");
        Equal("metric_snapshot_v1", reviewCoverage.MetricSnapshotSchemaVersion, "metric schema");
        Equal("weekly_lab_v1", reviewCoverage.WeeklyLabSchemaVersion, "weekly lab schema");
        Equal("metrics_v1", reviewCoverage.MetricAlgorithmVersion, "metric algorithm");
        Equal("0.666666666666666667", reviewCoverage.ValueDecimal, "review coverage ratio");
        Equal(3, reviewCoverage.CandidateEpisodeCount, "review candidate count");
        Equal(2, reviewCoverage.EligibleEpisodeCount, "review eligible count");
        Equal(1, reviewCoverage.ExcludedEpisodeCount, "review excluded count");
        Equal("EXPLORATORY", reviewCoverage.EvidenceLabel, "sample guardrail");
        Require(reviewCoverage.ExcludedEpisodeRefsJson.Contains("REVIEW_MISSING", StringComparison.Ordinal), "missing review is traceable");
        Require(reviewCoverage.SourceReviewRevisionIdsJson.Contains("review_revision_id", StringComparison.Ordinal), "review source IDs are pinned");

        MetricSnapshotRecord adherence = snapshots.Single(s => s.MetricId == "plan_adherence_rate");
        Equal("0.5", adherence.ValueDecimal, "adherence ratio");
        Require(adherence.ExclusionReasonCountsJson.Contains("REVIEW_MISSING", StringComparison.Ordinal), "adherence exclusions name review missing");

        MetricSnapshotRecord contextCoverage = snapshots.Single(s => s.MetricId == "context_coverage_counts" && s.Phase == "ENTRY" && s.Timeframe == "5m");
        Equal("OBJECT", contextCoverage.ValueType, "context coverage is object metric");
        Require(contextCoverage.ValueObjectJson?.Contains("CONTEXT_MISSING", StringComparison.Ordinal) == true, "context coverage records missing context");

        DashboardResponse dashboard = await app.GetDashboardAsync(actor);
        Equal(3, dashboard.Episodes.Count, "dashboard episodes");
        Equal(2, dashboard.Reviews.Count, "dashboard reviews");
        Equal(snapshots.Count, dashboard.MetricSnapshots.Count, "dashboard metrics");
        Require(dashboard.Episodes.Any(e => e.FeeConversions.Count > 0 && e.LedgerEntries.Count > 0), "dashboard includes accounting breakdown");
        Require(dashboard.DataQuality.ExclusionBanners.Any(b => b.Contains("REVIEW_MISSING", StringComparison.Ordinal)), "dashboard surfaces exclusion banner");
    }

    private static async Task<TradePlanRevisionRecord> ArmPlan(TradeProofApp app, ActorContext actor, string tradingAccountId, string setupRevisionId, string symbol, string suffix) =>
        Ok(await app.ArmPlanAsync(actor, new ArmPlanRequest(
            tradingAccountId,
            symbol,
            setupRevisionId,
            "90",
            "110",
            "80",
            "10",
            4,
            "Phase 5 test plan",
            7200,
            $"phase5-plan-{suffix}")), "arm plan");

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
            $"phase5-reserve-{suffix}")), "reserve csv");
        Ok(await app.RecordReservedBytesAsync(actor, new RecordReservedBytesRequest(
            reservation.ObjectIngestReservationId,
            reservation.WriteCapabilityId,
            Encoding.UTF8.GetBytes(csv),
            $"phase5-record-{suffix}")), "record csv");
        UploadTransferResponse transfer = Ok(await app.TransferRawUploadAsync(actor, new TransferRawUploadRequest(
            reservation.ObjectIngestReservationId,
            $"phase5-transfer-{suffix}")), "transfer csv");
        UploadValidationResponse validation = Ok(await app.ValidateUploadAsync(actor, new ValidateUploadRequest(
            transfer.Upload.UploadId,
            $"phase5-validate-{suffix}")), "validate csv");
        ImportPreviewRecord preview = validation.Preview ?? throw new InvalidOperationException("expected preview");
        return Ok(await app.ConfirmImportAsync(actor, new ConfirmImportRequest(
            preview.ImportPreviewId,
            preview.PreviewSummarySha256,
            $"phase5-confirm-{suffix}")), "confirm csv");
    }

    private static async Task<(TradeProofApp App, FixedTradeProofClock Clock, BootstrapResponse Bootstrap, ActorContext Actor)> NewWorkspace()
    {
        FixedTradeProofClock clock = new(StartAt);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"local-owner-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        return (app, clock, bootstrap, app.ActorFromBootstrap(bootstrap, identity));
    }

    private static byte[] PngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

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
