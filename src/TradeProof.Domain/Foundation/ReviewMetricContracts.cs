namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string Attachment = "attachment_v1";
    public const string AttachmentContentVersion = "attachment_content_version_v1";
    public const string Review = "review_v1";
    public const string ReviewRevision = "review_revision_v1";
    public const string ReviewRevisionAttachment = "review_revision_attachment_v1";
    public const string ReviewTaxonomy = "review_taxonomy_v1";
    public const string ReviewTaxonomyPublishEvent = "review_taxonomy_publish_event_v1";
    public const string MetricSnapshot = "metric_snapshot_v1";
    public const string WeeklyLab = "weekly_lab_v1";
    public const string MetricsAlgorithm = "metrics_v1";
    public const string MetricsDecimal = "metrics_decimal_v1";

    public const string AttachmentDelete = "ATTACHMENT_DELETE";
    public const string Metrics = "METRICS";
}

public sealed record ReserveReviewAttachmentRequest(
    string TradingAccountId,
    string UploadKind,
    string IdempotencyKey);

public sealed record ValidateAttachmentUploadRequest(string UploadId, string IdempotencyKey);

public sealed record DeleteAttachmentRequest(string AttachmentId, string IdempotencyKey);

public sealed record AttachmentValidationResponse(UploadRecord Upload, AttachmentRecord? Attachment, string? SafeErrorCode);

public sealed record AttachmentDeleteResponse(
    AttachmentRecord Attachment,
    AttachmentTombstoneRecord Tombstone,
    UploadObjectAbsenceVerificationRecord AbsenceVerification);

public sealed record AttachmentRecord(
    string AttachmentId,
    string WorkspaceId,
    string SourceUploadId,
    string AttachmentKind,
    string State,
    string ScanStatus,
    string ContentSha256,
    long SizeBytes,
    string AttachmentContentVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? DeletedAt,
    string? SafeErrorCode);

public sealed record AttachmentStateEventRecord(
    string AttachmentStateEventId,
    string WorkspaceId,
    string AttachmentId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt,
    string? SafeReasonCode);

public sealed record AttachmentTombstoneRecord(
    string AttachmentTombstoneId,
    string WorkspaceId,
    string AttachmentId,
    string SourceUploadId,
    string LastKnownContentSha256,
    DateTimeOffset DeletedAt,
    string AbsenceVerificationId);

public sealed record CompleteEpisodeReviewRequest(
    string EpisodeId,
    int ExpectedEpisodeProjectionVersion,
    string ExitReason,
    string? ExitReasonOtherText,
    bool RuleBreach,
    IReadOnlyList<string> BreachTypeIds,
    string? BreachOtherText,
    bool StopMovedAway,
    bool RiskExceeded,
    IReadOnlyDictionary<string, bool> RequiredChecklistResults,
    string? Emotion,
    string? Lesson,
    string? AttachmentId,
    string IdempotencyKey);

public sealed record ReviseEpisodeReviewRequest(
    string ReviewId,
    int ExpectedEpisodeProjectionVersion,
    int ExpectedRevisionNo,
    string ExitReason,
    string? ExitReasonOtherText,
    bool RuleBreach,
    IReadOnlyList<string> BreachTypeIds,
    string? BreachOtherText,
    bool StopMovedAway,
    bool RiskExceeded,
    IReadOnlyDictionary<string, bool> RequiredChecklistResults,
    string? Emotion,
    string? Lesson,
    string? AttachmentId,
    string IdempotencyKey);

public sealed record EpisodeReviewResult(ReviewRecord Review, ReviewRevisionRecord Revision);

public sealed record ReviewRecord(
    string ReviewId,
    string EpisodeId,
    string WorkspaceId,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset CompletedAt);

public sealed record ReviewRevisionRecord(
    string ReviewRevisionId,
    string WorkspaceId,
    string ReviewId,
    int RevisionNo,
    int EpisodeProjectionVersion,
    DateTimeOffset RecordedAt,
    string RecordedByUserId,
    string IdempotencyKey,
    string ExitReason,
    string ExitReasonTaxonomyVersion,
    string? ExitReasonOtherText,
    bool RuleBreach,
    string BreachTaxonomyVersion,
    IReadOnlyList<string> BreachTypeIds,
    string? BreachOtherText,
    bool StopMovedAway,
    bool RiskExceeded,
    string RequiredChecklistResultsJson,
    string? Emotion,
    string? EmotionTaxonomyVersion,
    string? Lesson,
    string ContentSha256);

public sealed record ReviewRevisionAttachmentRecord(
    string ReviewRevisionId,
    string WorkspaceId,
    string AttachmentId,
    string Role,
    int Ordinal,
    string AttachmentContentSha256,
    string AttachmentContentVersionId,
    DateTimeOffset CreatedAt);

public sealed record ReviewTaxonomyVersionRecord(
    string TaxonomyVersion,
    string TaxonomyType,
    string ContentSha256,
    DateTimeOffset PublishedAt);

public sealed record ReviewTaxonomyItemRecord(
    string TaxonomyVersion,
    string TaxonomyType,
    string ItemId,
    string LabelVi,
    int ItemOrder);

public sealed record ReviewTaxonomyPublishEventRecord(
    string TaxonomyPublishEventId,
    string TaxonomyType,
    int EventSequence,
    string TaxonomyVersion,
    DateTimeOffset RecordedAt,
    string ContentSha256);

public sealed record PublishMetricSnapshotsRequest(
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    string IdempotencyKey);

public sealed record MetricSnapshotRecord(
    string MetricSnapshotId,
    string WorkspaceId,
    string WeeklyCohortId,
    string WeeklyCohortInputRevisionId,
    string MetricSnapshotSchemaVersion,
    string WeeklyLabSchemaVersion,
    string MetricId,
    string MetricFormulaVersion,
    string MetricAlgorithmVersion,
    string EligibilityPolicyId,
    string DependencyVersionTupleHash,
    DateTimeOffset ReportingStartAtUtc,
    DateTimeOffset ReportingEndAtUtc,
    DateTimeOffset ReportingAsOfAt,
    string DimensionJson,
    string? Phase,
    string? Timeframe,
    string ValueType,
    string? ValueDecimal,
    int? ValueInteger,
    long? ValueDurationMs,
    string? ValueIntervalJson,
    string? ValueObjectJson,
    string Unit,
    string? NumeratorDecimal,
    string? DenominatorDecimal,
    string? NullReason,
    string DisplayState,
    string ComputationStatus,
    int CandidateEpisodeCount,
    int EligibleEpisodeCount,
    int ExcludedEpisodeCount,
    string CandidateEpisodeRefsJson,
    string IncludedEpisodeRefsJson,
    string ExcludedEpisodeRefsJson,
    string ExclusionReasonCountsJson,
    string SourceReviewRevisionIdsJson,
    string SourceContextSnapshotIdsJson,
    string EvidenceLabel,
    string InputDigestSha256,
    DateTimeOffset ComputedAt,
    string? SupersedesMetricSnapshotId);

public sealed record EpisodeDashboardRecord(
    string EpisodeId,
    int ProjectionVersion,
    string State,
    string VenueSymbol,
    string PlanProofStatus,
    string AccountingQuality,
    IReadOnlyList<FeeConversionRecord> FeeConversions,
    IReadOnlyList<AccountingLedgerEntryRecord> LedgerEntries,
    string? ReviewState,
    string? CurrentReviewRevisionId);

public sealed record DashboardDataQualityRecord(IReadOnlyList<string> ExclusionBanners);
