namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string Context = "CONTEXT";
    public const string ContextAlgorithm = "mce-binance-spot-v1.0.0";
    public const string ContextParameterSet = "mce-default-v1";
    public const string MarketBarAsOfSelector = "market_bar_as_of_v1";
    public const string MarketDataFetcher = "binance-public-kline-local-v1";
    public const string MarketConversionCatalog = "market_conversion_catalog_v1";
}

public sealed record PublishMarketConversionCatalogRequest(
    IReadOnlyList<MarketConversionCatalogInput> Pairs,
    string IdempotencyKey);

public sealed record MarketConversionCatalogInput(
    string VenueSymbol,
    string BaseAsset,
    string QuoteAsset,
    bool ConversionSupported);

public sealed record MarketConversionCatalogVersionRecord(
    string CatalogVersion,
    string VenueSymbol,
    string BaseAsset,
    string QuoteAsset,
    string Purpose,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidToExclusive,
    bool ConversionSupported,
    string ContentSha256,
    DateTimeOffset PublishedAt);

public sealed record MarketConversionCatalogPublishResponse(
    string CatalogVersion,
    IReadOnlyList<MarketConversionCatalogVersionRecord> Pairs);

public sealed record RecordMarketBarsRequest(
    string Symbol,
    string Timeframe,
    IReadOnlyList<MarketBarInput> Bars,
    string IdempotencyKey);

public sealed record MarketBarInput(DateTimeOffset OpenAt, string Close, string Volume);

public sealed record MarketDataIngestionBatchRecord(
    string IngestionBatchId,
    string SourceVenue,
    string ProductType,
    string SourceBaseUrl,
    string FetcherVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status);

public sealed record MarketDataSourceRequestRecord(
    string SourceRequestId,
    string IngestionBatchId,
    int RetryAttempt,
    string SourceBaseUrl,
    string HttpMethod,
    string Path,
    string Symbol,
    string Timeframe,
    int TimeZone,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int Limit,
    DateTimeOffset RequestedAt,
    DateTimeOffset? FetchedAt,
    int? HttpStatus,
    string? ResponseSha256,
    int? ResponseRowCount,
    string RequestMetadataHash);

public sealed record MarketBarRevisionRecord(
    string MarketBarRevisionId,
    string SourceVenue,
    string ProductType,
    string Symbol,
    string Timeframe,
    DateTimeOffset OpenAt,
    DateTimeOffset BarEndExclusive,
    string Close,
    string Volume,
    string ContentSha256,
    DateTimeOffset CreatedAt);

public sealed record MarketBarSourceObservationRecord(
    string SourceObservationId,
    string SourceRequestId,
    string MarketBarRevisionId,
    int ResponseRowIndex,
    int ObservationSequence);

public sealed record ContextAlgorithmReleaseRecord(
    string ContextAlgorithmReleaseId,
    string AlgorithmVersion,
    string ParameterSetId,
    string CalculationContractVersion,
    string CalculationContractSha256,
    string ImplementationArtifactSha256,
    string ParameterPayloadSha256,
    string ReleaseSha256,
    DateTimeOffset RegisteredAt);

public sealed record ContextEpisodeTriggerRecord(
    string ContextEpisodeTriggerId,
    string WorkspaceId,
    string EpisodeId,
    int ProjectionVersion,
    string Phase,
    string EventFillId,
    int SourceEventSequence,
    string ContentSha256,
    DateTimeOffset CreatedAt);

public sealed record RequestManualContextRecomputeRequest(
    string EpisodeId,
    int ProjectionVersion,
    string Phase,
    string Timeframe,
    int SourceEventSequence,
    string AlgorithmVersion,
    string ParameterSetId,
    string IdempotencyKey);

public sealed record ManualContextRecomputeRequestRecord(
    string ManualContextRecomputeRequestId,
    string WorkspaceId,
    string EpisodeId,
    int ProjectionVersion,
    string Phase,
    string Timeframe,
    int SourceEventSequence,
    string EventFillId,
    string AlgorithmVersion,
    string ParameterSetId,
    string ActorUserId,
    string IdempotencyKey,
    string RequestSha256,
    DateTimeOffset RequestedAt);

public sealed record ComputeContextSnapshotsRequest(
    string EpisodeId,
    int ProjectionVersion,
    string IdempotencyKey);

public sealed record ContextSnapshotRecord(
    string SnapshotId,
    string WorkspaceId,
    string EpisodeId,
    int ProjectionVersion,
    int SnapshotRevisionNo,
    string Phase,
    string EventFillId,
    int EventSequence,
    DateTimeOffset EventAt,
    DateTimeOffset EventTimeEndExclusive,
    string EventTimestampPrecision,
    string? ReferencePrice,
    string Venue,
    string ProductType,
    string Symbol,
    string Timeframe,
    string Timezone,
    DateTimeOffset AsOfAt,
    DateTimeOffset CutoffAt,
    DateTimeOffset? TargetBarOpenAt,
    int? HourOfWeek,
    DateTimeOffset? SessionStartAt,
    string? Rvol,
    string? EffortPercentile,
    string? VolumeRobustZ,
    string? VolumeAnomalyCode,
    string? NormalizedTrueRange,
    string? ResponsePercentile,
    string? RangeRobustZ,
    string? RangeAnomalyCode,
    string? SessionVwap,
    string? VwapDistanceBps,
    string? EffortResponseCode,
    string? EfficiencyRatio20,
    string? RealizedVol20,
    string? RealizedVolPercentile,
    string? RegimeCode,
    string Quality,
    IReadOnlyList<string> QualityReasons,
    IReadOnlyList<string> MissingIntervals,
    string CoreCoverage,
    string? SessionCoverage,
    string BaselineCoverage,
    int BaselineDistinctWeeks,
    bool AggregationEligible,
    string AlgorithmVersion,
    string ParameterSetId,
    IReadOnlyList<string> InputBarRevisionIds,
    IReadOnlyList<string> InputBarSourceObservationIds,
    IReadOnlyList<string?> InputBarResolutionIds,
    IReadOnlyList<string> SourceRequestIds,
    IReadOnlyList<string> SourceIngestionBatchIds,
    string InputHash,
    string ProvenanceHash,
    DateTimeOffset ComputedAt,
    string? SupersedesSnapshotId,
    string? RecomputeReason);
