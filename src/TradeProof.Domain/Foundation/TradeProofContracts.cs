using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TradeProof.Domain.Foundation;

public static partial class ContractVersions
{
    public const string SetupPreset = "setup_preset_v1";
    public const string SetupLabelKey = "setup_label_key_v1";
    public const string PlanChecklist = "plan_checklist_v1";
    public const string ProductMeasurementRun = "product_measurement_run_v1";
    public const string ProductAnalyticsEvent = "product_analytics_event_v1";
    public const string TenantControlJobPayload = "tenant_control_job_payload_v1";
    public const string TenantWorkItemTerminalMarker = "tenant_work_item_terminal_marker_v1";

    public const string ProductMeasurementTimeout = "PRODUCT_MEASUREMENT_TIMEOUT";

    [GeneratedRegex("^(0|[1-9][0-9]{0,19})(\\.[0-9]{1,18})?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalRegex();

    public static string CanonicalizeDecimal(string value, int maxIntegerDigits, int maxFractionDigits, bool requirePositive)
    {
        if (!DecimalRegex().IsMatch(value))
        {
            throw new TradeProofException("INVALID_DECIMAL");
        }

        string[] parts = value.Split('.', 2);
        if (parts[0].Length > maxIntegerDigits || (parts.Length == 2 && parts[1].Length > maxFractionDigits))
        {
            throw new TradeProofException("DECIMAL_OVERFLOW");
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed))
        {
            throw new TradeProofException("INVALID_DECIMAL");
        }

        if (requirePositive && parsed <= 0)
        {
            throw new TradeProofException("DECIMAL_MUST_BE_POSITIVE");
        }

        string canonical = parsed.ToString("0.##################", CultureInfo.InvariantCulture);
        return canonical == "-0" ? "0" : canonical;
    }

    public static string Sha256CanonicalJson<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NormalizeSetupLabelKey(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new TradeProofException("SETUP_LABEL_REQUIRED");
        }

        string normalized = label.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length is < 1 or > 60)
        {
            throw new TradeProofException("SETUP_LABEL_LENGTH_INVALID");
        }

        return normalized.ToUpperInvariant();
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}

public sealed class TradeProofException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}

public sealed record ManagedIdentity(string Issuer, string Subject, string? DisplayName);

public sealed record ActorContext(
    string ActorUserId,
    string WorkspaceId,
    string TradingAccountId,
    string Issuer,
    string Subject);

public sealed record UserRecord(string UserId, DateTimeOffset CreatedAt);

public sealed record UserIdentityRecord(
    string IdentityId,
    string UserId,
    string Issuer,
    string Subject,
    string ProviderMode,
    string IdentityProviderRegistrationId,
    int IdentityGeneration,
    DateTimeOffset CreatedAt);

public sealed record WorkspaceRecord(
    string WorkspaceId,
    string OwnerUserId,
    string LifecycleState,
    int DeletionGuardGeneration,
    string Timezone,
    DateTimeOffset CreatedAt);

public sealed record TradingAccountRecord(
    string TradingAccountId,
    string WorkspaceId,
    string Venue,
    string ProductType,
    string ReportingCurrency,
    string DisplayName,
    DateTimeOffset CreatedAt);

public sealed record AuditEventRecord(
    string AuditEventId,
    string Branch,
    string EventType,
    string? WorkspaceId,
    string? ActorUserId,
    string SafeCode,
    DateTimeOffset RecordedAt);

public sealed record SetupPresetRevisionRecord(
    string SetupPresetId,
    string RevisionId,
    string WorkspaceId,
    int RevisionNo,
    string SchemaVersion,
    string Label,
    string LabelKey,
    IReadOnlyList<ChecklistItemRecord> Checklist,
    bool IsSystem,
    bool IsActive,
    DateTimeOffset RecordedAt);

public sealed record ChecklistItemRecord(string ChecklistItemId, string Label, bool Required);

public sealed record IdempotencyReceiptRecord(
    string WorkspaceId,
    string CommandType,
    string IdempotencyKey,
    string RequestSha256,
    string ResponseJson,
    DateTimeOffset RecordedAt);

public sealed record TenantControlJobRecord(
    string TenantControlJobId,
    string WorkspaceId,
    long WorkSequence,
    string WorkType,
    string SubjectType,
    string SubjectKeyJson,
    string PayloadSchemaVersion,
    string PayloadDigestProfile,
    string PayloadSha256,
    string? PayloadJson,
    string OperationIdempotencyKey,
    int DeletionGuardGeneration,
    string State,
    bool Compacted,
    DateTimeOffset CreatedAt);

public sealed record TenantWorkItemFenceRecord(
    string TenantWorkItemFenceId,
    string TenantControlJobId,
    string WorkspaceId,
    long WorkSequence,
    string State,
    DateTimeOffset CreatedAt);

public sealed record TenantWorkItemFenceEventRecord(
    string TenantWorkItemFenceEventId,
    string TenantWorkItemFenceId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt);

public sealed record TenantExternalOperationLeaseRecord(
    string TenantExternalOperationLeaseId,
    string TenantControlJobId,
    string ProviderLookupKey,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

public sealed record TenantWorkItemTerminalMarkerRecord(
    string TenantWorkItemTerminalMarkerId,
    string TenantControlJobId,
    string WorkspaceId,
    long WorkSequence,
    string WorkType,
    string OperationPayloadSchemaVersion,
    string TerminalMarkerDigestProfile,
    string PayloadDigestProfile,
    string PayloadSha256,
    string ResultCode,
    DateTimeOffset TerminalAt);

public sealed record ProviderDispatchPlan(bool RequiresExternalLease, string ProviderLookupKey);

public sealed record ProductMeasurementRunRecord(
    string MeasurementRunId,
    string WorkspaceId,
    string Feature,
    string Mode,
    int? PracticeIndex,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset DeadlineAt,
    DateTimeOffset? TerminalAt,
    string? AbandonReason,
    string TimeoutTenantControlJobId);

public sealed record ProductMeasurementRunEventRecord(
    string ProductMeasurementRunEventId,
    string MeasurementRunId,
    string WorkspaceId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt);

public sealed record ProductAnalyticsEventRecord(
    string ProductAnalyticsEventId,
    string WorkspaceId,
    string EventType,
    string SourceRecordKeyJson,
    string PayloadJson,
    DateTimeOffset RecordedAt);

public sealed record TradePlanHeaderRecord(
    string TradePlanId,
    string WorkspaceId,
    string TradingAccountId,
    string Symbol,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record TradePlanRevisionRecord(
    string TradePlanRevisionId,
    string TradePlanId,
    string WorkspaceId,
    int RevisionNo,
    string SetupPresetRevisionId,
    string EntryZoneLow,
    string EntryZoneHigh,
    string InitialStop,
    string PlannedRiskUsdt,
    int Confidence,
    string? Thesis,
    IReadOnlyList<ChecklistItemRecord> Checklist,
    DateTimeOffset SubmittedAt,
    string ContentSha256);

public sealed record TradePlanEventRecord(
    string TradePlanEventId,
    string TradePlanId,
    string WorkspaceId,
    int EventSequence,
    string EventType,
    DateTimeOffset RecordedAt);

public sealed record BootstrapResponse(
    string UserId,
    string WorkspaceId,
    string TradingAccountId,
    string Timezone,
    IReadOnlyList<SetupPresetRevisionRecord> SetupPresets);

public sealed record CommandResult<T>(bool Succeeded, string? ErrorCode, T? Value)
{
    public static CommandResult<T> Ok(T value) => new(true, null, value);
    public static CommandResult<T> Fail(string code) => new(false, code, default);
}
