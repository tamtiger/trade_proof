using System.Security.Cryptography;
using System.Text;

namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string UploadAttachment = "upload_attachment_v1";
    public const string ObjectIngestReservation = "object_ingest_reservation_v1";
    public const string ImportPreview = "import_preview_v1";
    public const string StagedFill = "staged_fill_v1";
    public const string BinanceSpotTradeHistoryCsv = "binance_spot_trade_history_csv_v1";

    public const string ObjectIngestFinalize = "OBJECT_INGEST_FINALIZE";
    public const string UploadValidate = "UPLOAD_VALIDATE";
    public const string UploadPurge = "UPLOAD_PURGE";
    public const string Import = "IMPORT";

    public static readonly IReadOnlySet<string> RegisteredWorkTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        ProductMeasurementTimeout,
        ObjectIngestFinalize,
        UploadValidate,
        UploadPurge,
        Import
    };

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string Sha256Utf8(string value) => Sha256Hex(Encoding.UTF8.GetBytes(value));
}

public sealed record ReserveRawUploadRequest(
    string TradingAccountId,
    string AdapterContractVersion,
    string UploadKind,
    string IdempotencyKey);

public sealed record RecordReservedBytesRequest(
    string ObjectIngestReservationId,
    string WriteCapabilityId,
    byte[] Bytes,
    string IdempotencyKey);

public sealed record TransferRawUploadRequest(string ObjectIngestReservationId, string IdempotencyKey);

public sealed record ValidateUploadRequest(string UploadId, string IdempotencyKey);

public sealed record ConfirmImportRequest(string ImportPreviewId, string PreviewSummarySha256, string IdempotencyKey);

public sealed record PurgeUploadRequest(string UploadId, string IdempotencyKey);

public sealed record CreateStagedFillCandidateRequest(
    string ImportBatchId,
    int SourceRowNumber,
    string VenueSymbol,
    string Side,
    string ExecutedAt,
    string PriceQuotePerBase,
    string ExecutedQtyBase,
    string GrossAmountQuote,
    string FeeQty,
    string FeeAsset);

public sealed record ObjectIngestReservationRecord(
    string ObjectIngestReservationId,
    string WorkspaceId,
    string TradingAccountId,
    string Purpose,
    string ReservedUploadId,
    string? ReservedAttachmentId,
    string ExpectedUploadKind,
    string AdapterContractVersion,
    int LeaseGeneration,
    string ProviderObjectKeySha256,
    string WriteCapabilityId,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset WriteExpiresAt,
    DateTimeOffset AbsenceDueAt,
    DateTimeOffset? WriteCapabilityConsumedAt,
    DateTimeOffset? TransferredAt,
    string FinalizeTenantControlJobId);

public sealed record ObjectIngestReservationEventRecord(
    string ObjectIngestReservationEventId,
    string ObjectIngestReservationId,
    string WorkspaceId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt,
    string? SafeReasonCode);

public sealed record ProviderWriteResult(
    string ObjectIngestReservationId,
    string ProviderObjectVersionId,
    string ContentSha256,
    long SizeBytes,
    DateTimeOffset RecordedAt);

public sealed record UploadRecord(
    string UploadId,
    string WorkspaceId,
    string TradingAccountId,
    string Kind,
    string AdapterContractVersion,
    string State,
    string FileSha256,
    long FileSizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ForcedPurgeAt,
    DateTimeOffset PurgeDueAt,
    string SourceObjectIngestReservationId,
    int LeaseGeneration,
    string ValidateTenantControlJobId,
    string PurgeTenantControlJobId,
    string? SafeErrorCode,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? PurgedAt);

public sealed record UploadStateEventRecord(
    string UploadStateEventId,
    string WorkspaceId,
    string UploadId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt,
    string ActorType,
    string? SafeReasonCode,
    string? ObjectAbsenceVerificationId);

public sealed record UploadObjectLeaseRecord(
    string UploadObjectLeaseId,
    string WorkspaceId,
    string UploadId,
    int LeaseGeneration,
    string ProviderObjectVersionId,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TerminalAt);

public sealed record UploadObjectAbsenceVerificationRecord(
    string UploadObjectAbsenceVerificationId,
    string WorkspaceId,
    string UploadId,
    int LeaseGeneration,
    string LastKnownSha256,
    DateTimeOffset VerifiedAbsentAt);

public sealed record UploadTransferResponse(
    UploadRecord Upload,
    TenantControlJobRecord ValidateJob,
    TenantControlJobRecord PurgeJob,
    UploadObjectLeaseRecord Lease);

public sealed record UploadValidationResponse(UploadRecord Upload, ImportPreviewRecord? Preview, string? SafeErrorCode);

public sealed record UploadPurgeResponse(UploadRecord Upload, UploadObjectAbsenceVerificationRecord AbsenceVerification);

public sealed record ImportPreviewRecord(
    string ImportPreviewId,
    string WorkspaceId,
    string UploadId,
    string TradingAccountId,
    string SchemaVersion,
    string AdapterContractVersion,
    string State,
    int DataRows,
    IReadOnlyList<string> Symbols,
    DateTimeOffset? FirstTradeAt,
    DateTimeOffset? LastTradeAt,
    string PreviewSummarySha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedImportBatchId,
    IReadOnlyList<SafeRowErrorRecord> SafeErrors);

public sealed record SafeRowErrorRecord(int SourceRowNumber, string Code, string? Field, int OccurrenceCount);

public sealed record ImportBatchRecord(
    string ImportBatchId,
    string WorkspaceId,
    string TradingAccountId,
    string SourceUploadId,
    string SourceImportPreviewId,
    string SourcePreviewSchemaVersion,
    string SourcePreviewSummarySha256,
    string AdapterContractVersion,
    DateTimeOffset ConfirmedAt,
    string Status,
    int? DataRows,
    int ReconciledRows,
    int DuplicateRows,
    int AccountingPendingRows,
    int QuarantinedRows,
    string? FileErrorCode,
    string ImportTenantControlJobId);

public sealed record ImportProgressResponse(
    string BatchId,
    string UploadId,
    string PreviewId,
    string Status,
    int? DataRows,
    int ReconciledRows,
    int DuplicateRows,
    int AccountingPendingRows,
    int QuarantinedRows,
    IReadOnlyList<SafeRowErrorRecord> SafeErrors,
    IReadOnlyList<string> AllowedDispositions);

public sealed record StagedFillRecord(
    string StagedFillId,
    string WorkspaceId,
    string TradingAccountId,
    string ImportBatchId,
    int SourceRowNumber,
    string StagedFillSchemaVersion,
    string InstrumentCatalogVersion,
    string Venue,
    string ProductType,
    string VenueSymbol,
    string Side,
    DateTimeOffset ExecutedAt,
    string PriceQuotePerBase,
    string ExecutedQtyBase,
    string GrossAmountQuote,
    string FeeQty,
    string FeeAsset,
    string SourceRowFingerprintSha256,
    string CanonicalSignatureSha256,
    DateTimeOffset CreatedAt);

public sealed record StagedFillDispositionRecord(
    string StagedFillDispositionId,
    string WorkspaceId,
    string StagedFillId,
    string Outcome,
    string? NormalizedFillId,
    string? DuplicateOfFillId,
    DateTimeOffset RecordedAt);
