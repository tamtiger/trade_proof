namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string WeeklyLabRenderer = "weekly_lab_renderer_v1";
    public const string BehavioralExperiment = "behavioral_experiment_v1";
    public const string WeeklyReviewCompletion = "weekly_review_completion_v1";
    public const string WeeklyLabExportProjection = "weekly_lab_export_projection_v1";
    public const string ProductMetrics = "product_metrics_v1";
    public const string WorkspaceProductMetricSnapshot = "workspace_product_metric_snapshot_v1";
    public const string InternalAggregateProductMetricSnapshot = "internal_aggregate_product_metric_snapshot_v1";
    public const string ProductAnalyticsExternal = "product_analytics_external_v1";
    public const string TradeProofExport = "tradeproof_export_v1";
    public const string TradeProofExportJob = "tradeproof_export_job_v1";
    public const string TradeProofExportManifest = "tradeproof_export_manifest_v1";
    public const string TradeProofExportRoundTrip = "tradeproof_export_round_trip_v1";
    public const string ExportSlaEnvelope = "export_sla_envelope_v1";
    public const string SpreadsheetEscape = "spreadsheet_escape_v1";
    public const string WorkspaceDeletion = "workspace_deletion_v1";

    public const string CohortLock = "COHORT_LOCK";
    public const string Report = "REPORT";
    public const string ProductMetric = "PRODUCT_METRIC";
    public const string AnalyticsDelivery = "ANALYTICS_DELIVERY";
    public const string AnalyticsPurge = "ANALYTICS_PURGE";
    public const string Export = "EXPORT";
    public const string ExportExpiry = "EXPORT_EXPIRY";
    public const string WorkspaceDeletionWork = "WORKSPACE_DELETION";
}

public sealed record PublishWeeklyLabRequest(
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    string IdempotencyKey);

public sealed record WeeklyLabPublicationResult(
    WeeklyCohortRecord Cohort,
    WeeklyCohortInputRevisionRecord InputRevision,
    WeeklyReportRevisionRecord ReportRevision,
    WeeklyCohortRecord NextCohort);

public sealed record WeeklyCohortRecord(
    string WeeklyCohortId,
    string WorkspaceId,
    string WeeklyLabSchemaVersion,
    string CohortType,
    string State,
    string WorkspaceTimezone,
    string CohortStartLocal,
    string CohortEndLocalExclusive,
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    DateTimeOffset LockedAt,
    string? PreviousWeeklyCohortId);

public sealed record WeeklyCohortInputRevisionRecord(
    string WeeklyCohortInputRevisionId,
    string WeeklyCohortId,
    string WorkspaceId,
    int RevisionNo,
    string WeeklyLabSchemaVersion,
    string Reason,
    string IdempotencyKey,
    DateTimeOffset ReportingAsOfAt,
    string DependencyVersionTupleJson,
    string DependencyVersionTupleHash,
    string EpisodeProjectionRefsJson,
    string ReviewRevisionRefsJson,
    string ContextRefMatrixJson,
    string MetricSnapshotRefsJson,
    string InputDigestSha256);

public sealed record WeeklyReportRevisionRecord(
    string WeeklyReportRevisionId,
    string WeeklyReportId,
    string WorkspaceId,
    string WeeklyCohortId,
    string WeeklyCohortInputRevisionId,
    int RevisionNo,
    string Status,
    string WeeklyLabSchemaVersion,
    string RendererVersion,
    string Locale,
    string MetricSnapshotIdsJson,
    string SectionPayloadJson,
    string RenderedSectionsJson,
    string ContentSha256,
    DateTimeOffset PublishedAt,
    string? SupersedesReportRevisionId,
    string NextWeeklyCohortId);

public sealed record ProposeBehavioralExperimentRequest(
    string WeeklyReportRevisionId,
    string ExperimentTypeId,
    string ProposalText,
    string IdempotencyKey);

public sealed record ConfirmBehavioralExperimentRequest(
    string BehavioralExperimentId,
    int ExpectedRevisionNo,
    string IdempotencyKey);

public sealed record CancelBehavioralExperimentRequest(
    string BehavioralExperimentId,
    int ExpectedRevisionNo,
    string Reason,
    string IdempotencyKey);

public sealed record BehavioralExperimentRevisionRecord(
    string BehavioralExperimentRevisionId,
    string BehavioralExperimentId,
    string WorkspaceId,
    int RevisionNo,
    string TaxonomyVersion,
    string ExperimentTypeId,
    string State,
    string TargetWeeklyCohortId,
    string SourceWeeklyReportRevisionId,
    string ProposalText,
    DateTimeOffset RecordedAt,
    string RecordedByUserId,
    string IdempotencyKey,
    string ContentSha256);

public sealed record CompleteWeeklyReviewRequest(
    string WeeklyCohortId,
    string WeeklyReportRevisionId,
    string BehavioralExperimentRevisionId,
    string IdempotencyKey);

public sealed record WeeklyReviewCompletionRecord(
    string WeeklyReviewCompletionId,
    string WorkspaceId,
    string SchemaVersion,
    string WeeklyCohortId,
    string WeeklyReportRevisionId,
    string BehavioralExperimentRevisionId,
    DateTimeOffset CompletedAt,
    string IdempotencyKey,
    string ContentSha256);

public sealed record RecordProductAnalyticsEventRequest(
    string EventType,
    string SourceRecordType,
    string SourceRecordId,
    DateTimeOffset OccurredAt,
    string IdempotencyKey);

public sealed record PublishWorkspaceProductMetricsRequest(
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    string IdempotencyKey);

public sealed record WorkspaceProductMetricSnapshotRecord(
    string WorkspaceProductMetricSnapshotId,
    string WorkspaceId,
    string SchemaVersion,
    string MetricDictionaryVersion,
    string MetricId,
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    DateTimeOffset ReportingAsOfAt,
    string ValueType,
    int? ValueInteger,
    string? ValueDecimal,
    string? NullReason,
    string SourceEventRefsJson,
    string InputDigestSha256);

public sealed record PublishInternalAggregateProductMetricRequest(
    IReadOnlyList<string> WorkspaceIds,
    string MetricId,
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    string IdempotencyKey);

public sealed record InternalAggregateProductMetricSnapshotRecord(
    string InternalAggregateProductMetricSnapshotId,
    string SchemaVersion,
    string MetricDictionaryVersion,
    string MetricId,
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    DateTimeOffset ReportingAsOfAt,
    int WorkspaceCount,
    string ValueType,
    int? ValueInteger,
    string? ValueDecimal,
    string? NullReason,
    string SourceWorkspaceRefsJson,
    string InputDigestSha256);

public sealed record ProjectExternalAnalyticsRequest(
    string ProductAnalyticsEventId,
    string IdempotencyKey);

public sealed record ExternalAnalyticsProjectionRecord(
    string ExternalAnalyticsProjectionId,
    string WorkspaceId,
    string ProductAnalyticsEventId,
    string SchemaVersion,
    string State,
    string PayloadJson,
    string PayloadSha256,
    DateTimeOffset ProjectedAt);

public sealed record PurgeExternalAnalyticsRequest(
    string ExternalAnalyticsProjectionId,
    string IdempotencyKey);

public sealed record ExternalAnalyticsPurgeRecord(
    string ExternalAnalyticsPurgeId,
    string WorkspaceId,
    string ExternalAnalyticsProjectionId,
    string WorkType,
    string State,
    string AbsenceDigestSha256,
    DateTimeOffset PurgedAt);

public sealed record RequestTradeProofExportRequest(
    string WeeklyReportRevisionId,
    DateTimeOffset ExportAsOfAt,
    string IdempotencyKey);

public sealed record TradeProofExportRecord(
    string TradeProofExportId,
    string WorkspaceId,
    string WeeklyReportRevisionId,
    string ExportSchemaVersion,
    string ExportJobSchemaVersion,
    string State,
    string ServiceClass,
    DateTimeOffset ExportAsOfAt,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt,
    string ManifestJson,
    string CsvEntriesJson,
    string ContentSha256,
    string ExportExpiryTenantControlJobId);

public sealed record ValidateExportRoundTripRequest(
    string TradeProofExportId,
    string IdempotencyKey);

public sealed record ExportRoundTripValidationRecord(
    string ExportRoundTripValidationId,
    string WorkspaceId,
    string TradeProofExportId,
    string ReaderProfileVersion,
    bool Passed,
    string CheckedContentSha256,
    DateTimeOffset ValidatedAt);

public sealed record ExpireExportRequest(
    string TradeProofExportId,
    string IdempotencyKey);

public sealed record ExportExpiryRecord(
    string ExportExpiryRecordId,
    string WorkspaceId,
    string TradeProofExportId,
    string WorkType,
    string State,
    string AbsenceDigestSha256,
    DateTimeOffset ExpiredAt);

public sealed record RequestWorkspaceDeletionRequest(string IdempotencyKey);

public sealed record CompleteWorkspaceDeletionRequest(
    string WorkspaceDeletionId,
    string IdempotencyKey);

public sealed record WorkspaceDeletionRecord(
    string WorkspaceDeletionId,
    string WorkspaceId,
    string SchemaVersion,
    string State,
    int GuardGeneration,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string RequestedByUserId,
    string ContentSha256);

public sealed record WorkspaceDeletionTargetRecord(
    string WorkspaceDeletionTargetId,
    string WorkspaceDeletionId,
    string WorkspaceId,
    int Ordinal,
    string TargetType,
    string State,
    DateTimeOffset UpdatedAt,
    string EvidenceSha256);

public sealed record WorkspaceDeletionTombstoneRecord(
    string WorkspaceDeletionTombstoneId,
    string WorkspaceDeletionId,
    string WorkspaceId,
    int GuardGeneration,
    DateTimeOffset TombstonedAt,
    string EvidenceSha256);

public sealed record WorkspaceDeletionResult(
    WorkspaceDeletionRecord Deletion,
    IReadOnlyList<WorkspaceDeletionTargetRecord> Targets,
    int CancelledTenantWorkCount,
    int RevokedExportCount);
