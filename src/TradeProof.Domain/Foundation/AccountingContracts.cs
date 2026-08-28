namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string NormalizedFill = "normalized_fill_v1";
    public const string EpisodeProjection = "episode_projection_v1";
    public const string PlanProof = "plan_proof_v1";
    public const string FeeConversion = "fee_conversion_v1";
    public const string WeightedAverageEpisode = "wac_episode_v1";
}

public sealed record ProcessImportRequest(string ImportBatchId, string IdempotencyKey);

public sealed record ImportRowRecord(
    string ImportRowId,
    string WorkspaceId,
    string TradingAccountId,
    string ImportBatchId,
    int SourceRowNumber,
    string RawRowSha256,
    string Status,
    string? SafeErrorCode,
    string? NormalizedFillId,
    string? DuplicateOfFillId,
    string? StagedFillId,
    DateTimeOffset RecordedAt);

public sealed record NormalizedFillRecord(
    string FillId,
    string WorkspaceId,
    string TradingAccountId,
    string ImportBatchId,
    string ImportRowId,
    int SourceRowNumber,
    string ImportContractVersion,
    string FillSchemaVersion,
    string InstrumentCatalogVersion,
    string Venue,
    string ProductType,
    string InstrumentId,
    string VenueSymbol,
    string BaseAsset,
    string QuoteAsset,
    string Side,
    DateTimeOffset ExecutedAt,
    string SourceTimestampPrecision,
    DateTimeOffset SourceTimeStart,
    DateTimeOffset SourceTimeEndExclusive,
    string PriceQuotePerBase,
    string ExecutedQtyBase,
    string GrossAmountQuote,
    string FeeQty,
    string FeeAsset,
    string CanonicalSignatureSha256,
    int OccurrenceIndex,
    string DedupKey,
    DateTimeOffset CreatedAt);

public sealed record FeeConversionRecord(
    string FeeConversionId,
    string WorkspaceId,
    string FillId,
    int ConversionVersion,
    string FeeAsset,
    string QuoteAsset,
    string FeeQty,
    string Status,
    string? Method,
    string? RateQuotePerFeeAsset,
    string? FeeValueQuote,
    DateTimeOffset? AsOfAt,
    string AlgorithmVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SupersededAt);

public sealed record TradeEpisodeHeaderRecord(
    string EpisodeId,
    string WorkspaceId,
    string TradingAccountId,
    string InstrumentId,
    string OpeningFillId,
    string OpeningFillDedupKey,
    DateTimeOffset CreatedAt);

public sealed record TradeEpisodeProjectionRecord(
    string EpisodeId,
    int ProjectionVersion,
    string ProjectionAlgorithmVersion,
    string LedgerAlgorithmVersion,
    string WorkspaceId,
    string TradingAccountId,
    string InstrumentId,
    string QuoteAsset,
    string State,
    string FirstFillId,
    DateTimeOffset FirstFillAt,
    DateTimeOffset FirstFillTimeEndExclusive,
    string FirstFillTimestampPrecision,
    string? ClosedFillId,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ClosedTimeEndExclusive,
    string? ClosedTimestampPrecision,
    string? AssociatedPlanId,
    string? AssociatedPlanRevisionId,
    string? FrozenPlanRevisionId,
    string PlanProofStatus,
    string PlanProofReasonCode,
    string PlanProofRuleVersion,
    string PlanCandidateIdsJson,
    string PlanProofBasisJson,
    DateTimeOffset PlanProofResolvedAt,
    string? LateAssociationId,
    string? PlanMatchResolutionId,
    string PositionQtyBase,
    string OpenCostBasisQuote,
    string? AverageCostQuotePerBase,
    string GrossRealizedPnlQuote,
    string KnownFeeQuote,
    string? NetRealizedPnlQuote,
    string? PlannedInitialRiskQuote,
    string? RMultiple,
    string AccountingQuality,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SupersededAt);

public sealed record EpisodeFillAllocationRecord(
    string EpisodeId,
    string WorkspaceId,
    int ProjectionVersion,
    string FillId,
    int EventSequence,
    string PositionQtyBefore,
    string PositionQtyDelta,
    string PositionQtyAfter,
    string CostBasisBefore,
    string CostBasisDelta,
    string CostBasisAfter,
    string GrossRealizedDeltaQuote,
    string? FeeExpenseDeltaQuote);

public sealed record AccountingLedgerEntryRecord(
    string LedgerEntryId,
    string WorkspaceId,
    string EpisodeId,
    int ProjectionVersion,
    string FillId,
    int EntrySequence,
    string EntryType,
    DateTimeOffset OccurredAt,
    string Asset,
    string AssetQtyDelta,
    string QuoteAsset,
    string? QuoteValueDelta,
    string PositionQtyDeltaBase,
    string CostBasisDeltaQuote,
    string GrossRealizedDeltaQuote,
    string? FeeExpenseDeltaQuote,
    string? FeeConversionId,
    string AlgorithmVersion,
    DateTimeOffset CreatedAt);

public sealed record ImportEpisodeSummaryRecord(
    string EpisodeId,
    int ProjectionVersion,
    string State,
    string VenueSymbol,
    string PlanProofStatus,
    string AccountingQuality,
    string GrossRealizedPnlQuote,
    string? NetRealizedPnlQuote,
    string? RMultiple);
