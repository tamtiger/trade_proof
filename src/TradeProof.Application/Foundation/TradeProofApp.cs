using System.Text.Json;
using TradeProof.Domain.Foundation;

namespace TradeProof.Application.Foundation;

public interface ITradeProofClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemTradeProofClock : ITradeProofClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class FixedTradeProofClock(DateTimeOffset initial) : ITradeProofClock
{
    public DateTimeOffset UtcNow { get; private set; } = initial;

    public void Advance(TimeSpan interval) => UtcNow = UtcNow.Add(interval);
}

public sealed record CreateSetupPresetRequest(string Label, IReadOnlyList<ChecklistItemInput> Checklist, string IdempotencyKey);
public sealed record ReviseSetupPresetRequest(string SetupPresetId, string Label, IReadOnlyList<ChecklistItemInput> Checklist, string IdempotencyKey);
public sealed record SetupPresetCommandRequest(string SetupPresetId, string IdempotencyKey);
public sealed record ChecklistItemInput(string Label, bool Required);

public sealed record ArmPlanRequest(
    string TradingAccountId,
    string Symbol,
    string SetupPresetRevisionId,
    string EntryZoneLow,
    string EntryZoneHigh,
    string InitialStop,
    string PlannedRiskUsdt,
    int Confidence,
    string? Thesis,
    int? ExpiryDurationSeconds,
    string IdempotencyKey);

public sealed record RevisePlanRequest(
    string TradePlanId,
    string SetupPresetRevisionId,
    string EntryZoneLow,
    string EntryZoneHigh,
    string InitialStop,
    string PlannedRiskUsdt,
    int Confidence,
    string? Thesis,
    int? ExpiryDurationSeconds,
    string IdempotencyKey);

public sealed record PlanCommandRequest(string TradePlanId, string IdempotencyKey);

public sealed record StartProductMeasurementRequest(
    string Feature,
    string Mode,
    int? PracticeIndex,
    string IdempotencyKey);

public sealed record CompleteProductMeasurementRequest(string MeasurementRunId, string IdempotencyKey);
public sealed record AbandonProductMeasurementRequest(string MeasurementRunId, string Reason, string IdempotencyKey);

public sealed record DashboardResponse(
    BootstrapResponse Bootstrap,
    IReadOnlyList<TradePlanHeaderRecord> Plans,
    IReadOnlyList<ProductMeasurementRunRecord> MeasurementRuns,
    IReadOnlyList<EpisodeDashboardRecord> Episodes,
    IReadOnlyList<ReviewRecord> Reviews,
    IReadOnlyList<ReviewRevisionRecord> ReviewRevisions,
    IReadOnlyList<AttachmentRecord> Attachments,
    IReadOnlyList<MetricSnapshotRecord> MetricSnapshots,
    DashboardDataQualityRecord DataQuality,
    IReadOnlyList<AuditEventRecord> AuditEvents);

public sealed partial class TradeProofApp(ITradeProofClock clock)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, UserIdentityRecord> _identitiesByKey = [];
    private readonly Dictionary<string, UserRecord> _users = [];
    private readonly Dictionary<string, WorkspaceRecord> _workspaces = [];
    private readonly Dictionary<string, TradingAccountRecord> _accounts = [];
    private readonly Dictionary<string, IdempotencyReceiptRecord> _idempotency = [];
    private readonly Dictionary<string, long> _workSequences = [];
    private readonly Dictionary<string, TenantControlJobRecord> _jobs = [];
    private readonly Dictionary<string, TenantWorkItemFenceRecord> _fencesByJob = [];
    private readonly Dictionary<string, List<TenantWorkItemFenceEventRecord>> _fenceEvents = [];
    private readonly Dictionary<string, TenantWorkItemTerminalMarkerRecord> _markersByJob = [];
    private readonly List<TenantExternalOperationLeaseRecord> _leases = [];
    private readonly Dictionary<string, List<SetupPresetRevisionRecord>> _setupRevisions = [];
    private readonly Dictionary<string, TradePlanHeaderRecord> _plans = [];
    private readonly Dictionary<string, List<TradePlanRevisionRecord>> _planRevisions = [];
    private readonly Dictionary<string, List<TradePlanEventRecord>> _planEvents = [];
    private readonly Dictionary<string, ProductMeasurementRunRecord> _measurementRuns = [];
    private readonly Dictionary<string, List<ProductMeasurementRunEventRecord>> _measurementEvents = [];
    private readonly List<ProductAnalyticsEventRecord> _analyticsEvents = [];
    private readonly List<AuditEventRecord> _auditEvents = [];
    private long _id;

    private DateTimeOffset Now => clock.UtcNow;

    public Task<CommandResult<BootstrapResponse>> BootstrapAsync(ManagedIdentity? identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (identity is null || identity.Issuer.Length == 0 || identity.Subject.Length == 0)
            {
                RecordAudit("PRE_AUTH", "AUTHENTICATION_FAILED", null, null, "AUTH_REQUIRED");
                return Task.FromResult(CommandResult<BootstrapResponse>.Fail("AUTH_REQUIRED"));
            }

            string identityKey = $"{identity.Issuer}\u001F{identity.Subject}";
            if (!_identitiesByKey.TryGetValue(identityKey, out UserIdentityRecord? existing))
            {
                DateTimeOffset now = clock.UtcNow;
                string userId = NextId("usr");
                string identityId = NextId("idn");
                string workspaceId = NextId("wsp");
                string tradingAccountId = NextId("acct");

                _users[userId] = new UserRecord(userId, now);
                existing = new UserIdentityRecord(
                    identityId,
                    userId,
                    identity.Issuer,
                    identity.Subject,
                    "MANAGED_DEDICATED",
                    "dev-managed-header-v1",
                    1,
                    now);
                _identitiesByKey[identityKey] = existing;
                _workspaces[workspaceId] = new WorkspaceRecord(workspaceId, userId, "ACTIVE", 1, "Asia/Ho_Chi_Minh", now);
                _accounts[tradingAccountId] = new TradingAccountRecord(
                    tradingAccountId,
                    workspaceId,
                    "BINANCE",
                    "SPOT",
                    "USDT",
                    identity.DisplayName ?? "Binance Spot",
                    now);
                CreateSystemOtherPreset(workspaceId, now);
                RecordAudit("POST_AUTH", "BOOTSTRAP", workspaceId, userId, "BOOTSTRAP_CREATED");
            }
            else
            {
                WorkspaceRecord workspace = _workspaces.Values.Single(w => w.OwnerUserId == existing.UserId);
                RecordAudit("POST_AUTH", "BOOTSTRAP", workspace.WorkspaceId, existing.UserId, "BOOTSTRAP_REUSED");
            }

            return Task.FromResult(CommandResult<BootstrapResponse>.Ok(BuildBootstrap(existing.UserId)));
        }
    }

    public Task<DashboardResponse> GetDashboardAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            return Task.FromResult(new DashboardResponse(
                BuildBootstrap(actor.ActorUserId),
                _plans.Values.Where(p => p.WorkspaceId == actor.WorkspaceId).OrderByDescending(p => p.CreatedAt).ToList(),
                _measurementRuns.Values.Where(r => r.WorkspaceId == actor.WorkspaceId).OrderByDescending(r => r.StartedAt).ToList(),
                BuildDashboardEpisodes(actor.WorkspaceId),
                Reviews.Where(r => r.WorkspaceId == actor.WorkspaceId).ToList(),
                ReviewRevisions.Where(r => r.WorkspaceId == actor.WorkspaceId).ToList(),
                Attachments.Where(a => a.WorkspaceId == actor.WorkspaceId).ToList(),
                MetricSnapshots.Where(m => m.WorkspaceId == actor.WorkspaceId).ToList(),
                BuildDashboardDataQuality(actor.WorkspaceId),
                _auditEvents.Where(a => a.WorkspaceId == actor.WorkspaceId).OrderBy(a => a.RecordedAt).ToList()));
        }
    }

    public Task<CommandResult<SetupPresetRevisionRecord>> CreateSetupPresetAsync(
        ActorContext actor,
        CreateSetupPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CreateSetupPreset", request.IdempotencyKey, request, () =>
                {
                    string labelKey = ContractVersions.NormalizeSetupLabelKey(request.Label);
                    if (ActiveSetupPresets(actor.WorkspaceId).Any(p => p.LabelKey == labelKey))
                    {
                        throw new TradeProofException("SETUP_LABEL_CONFLICT");
                    }

                    if (ActiveSetupPresets(actor.WorkspaceId).Count(p => !p.IsSystem) >= 50)
                    {
                        throw new TradeProofException("SETUP_LIMIT_EXCEEDED");
                    }

                    DateTimeOffset now = clock.UtcNow;
                    string presetId = NextId("setup");
                    SetupPresetRevisionRecord revision = new(
                        presetId,
                        NextId("setuprev"),
                        actor.WorkspaceId,
                        1,
                        ContractVersions.SetupPreset,
                        request.Label.Trim(),
                        labelKey,
                        BuildChecklist(request.Checklist),
                        false,
                        true,
                        now);
                    _setupRevisions[presetId] = [revision];
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<SetupPresetRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<SetupPresetRevisionRecord>> ReviseSetupPresetAsync(
        ActorContext actor,
        ReviseSetupPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ReviseSetupPreset", request.IdempotencyKey, request, () =>
                {
                    SetupPresetRevisionRecord current = CurrentSetupRevision(actor.WorkspaceId, request.SetupPresetId);
                    if (current.IsSystem)
                    {
                        throw new TradeProofException("SYSTEM_PRESET_IMMUTABLE");
                    }

                    string labelKey = ContractVersions.NormalizeSetupLabelKey(request.Label);
                    if (ActiveSetupPresets(actor.WorkspaceId).Any(p => p.SetupPresetId != request.SetupPresetId && p.LabelKey == labelKey))
                    {
                        throw new TradeProofException("SETUP_LABEL_CONFLICT");
                    }

                    SetupPresetRevisionRecord revision = current with
                    {
                        RevisionId = NextId("setuprev"),
                        RevisionNo = current.RevisionNo + 1,
                        Label = request.Label.Trim(),
                        LabelKey = labelKey,
                        Checklist = BuildChecklist(request.Checklist),
                        IsActive = true,
                        RecordedAt = clock.UtcNow
                    };
                    _setupRevisions[request.SetupPresetId].Add(revision);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<SetupPresetRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<SetupPresetRevisionRecord>> ArchiveSetupPresetAsync(
        ActorContext actor,
        SetupPresetCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ToggleSetupPresetAsync(actor, request, false);
    }

    public Task<CommandResult<SetupPresetRevisionRecord>> ReactivateSetupPresetAsync(
        ActorContext actor,
        SetupPresetCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ToggleSetupPresetAsync(actor, request, true);
    }

    public Task<CommandResult<TradePlanRevisionRecord>> ArmPlanAsync(
        ActorContext actor,
        ArmPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "ArmPlan", request.IdempotencyKey, request, () =>
                {
                    ValidateTradingAccount(actor, request.TradingAccountId);
                    PlanFields fields = ValidatePlanFields(actor, request);
                    DateTimeOffset now = clock.UtcNow;
                    if (_plans.Values.Any(p => p.WorkspaceId == actor.WorkspaceId &&
                                               p.TradingAccountId == request.TradingAccountId &&
                                               p.Symbol == fields.Symbol &&
                                               p.State == "ARMED" &&
                                               now < p.ExpiresAt))
                    {
                        throw new TradeProofException("PLAN_ACTIVE_CONFLICT");
                    }

                    string planId = NextId("plan");
                    DateTimeOffset expiresAt = now.AddSeconds(request.ExpiryDurationSeconds ?? 86400);
                    _plans[planId] = new TradePlanHeaderRecord(planId, actor.WorkspaceId, request.TradingAccountId, fields.Symbol, "ARMED", now, expiresAt);
                    TradePlanRevisionRecord revision = CreatePlanRevision(actor.WorkspaceId, planId, 1, fields, now);
                    _planRevisions[planId] = [revision];
                    _planEvents[planId] = [new TradePlanEventRecord(NextId("planevt"), planId, actor.WorkspaceId, 1, "ARM", now)];
                    RecordAnalytics(actor.WorkspaceId, "plan_armed", new { trade_plan_revision_id = revision.TradePlanRevisionId }, new { symbol_quote_asset = "USDT" }, now);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<TradePlanRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<TradePlanRevisionRecord>> RevisePlanAsync(
        ActorContext actor,
        RevisePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "RevisePlan", request.IdempotencyKey, request, () =>
                {
                    TradePlanHeaderRecord header = GetOwnedPlan(actor, request.TradePlanId);
                    if (header.State != "ARMED")
                    {
                        throw new TradeProofException("PLAN_NOT_ARMED");
                    }

                    PlanFields fields = ValidatePlanFields(actor, new ArmPlanRequest(
                        header.TradingAccountId,
                        header.Symbol,
                        request.SetupPresetRevisionId,
                        request.EntryZoneLow,
                        request.EntryZoneHigh,
                        request.InitialStop,
                        request.PlannedRiskUsdt,
                        request.Confidence,
                        request.Thesis,
                        request.ExpiryDurationSeconds,
                        request.IdempotencyKey));

                    DateTimeOffset now = clock.UtcNow;
                    int nextRevision = _planRevisions[request.TradePlanId].Max(r => r.RevisionNo) + 1;
                    TradePlanRevisionRecord revision = CreatePlanRevision(actor.WorkspaceId, request.TradePlanId, nextRevision, fields, now);
                    _planRevisions[request.TradePlanId].Add(revision);
                    AddPlanEvent(header.TradePlanId, actor.WorkspaceId, "REVISE", now);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<TradePlanRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<TradePlanHeaderRecord>> CancelPlanAsync(
        ActorContext actor,
        PlanCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "CancelPlan", request.IdempotencyKey, request, () =>
                {
                    TradePlanHeaderRecord header = GetOwnedPlan(actor, request.TradePlanId);
                    if (header.State != "ARMED")
                    {
                        throw new TradeProofException("PLAN_NOT_ARMED");
                    }

                    DateTimeOffset now = clock.UtcNow;
                    TradePlanHeaderRecord cancelled = header with { State = "CANCELLED" };
                    _plans[header.TradePlanId] = cancelled;
                    AddPlanEvent(header.TradePlanId, actor.WorkspaceId, "CANCEL", now);
                    return cancelled;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<TradePlanHeaderRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<int> ExpirePlansAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            int expired = 0;
            foreach (TradePlanHeaderRecord header in _plans.Values.Where(p => p.WorkspaceId == actor.WorkspaceId && p.State == "ARMED" && clock.UtcNow >= p.ExpiresAt).ToList())
            {
                _plans[header.TradePlanId] = header with { State = "EXPIRED" };
                AddPlanEvent(header.TradePlanId, actor.WorkspaceId, "EXPIRE", clock.UtcNow);
                expired++;
            }

            return Task.FromResult(expired);
        }
    }

    public Task<CommandResult<ProductMeasurementRunRecord>> StartProductMeasurementAsync(
        ActorContext actor,
        StartProductMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "StartProductMeasurement", request.IdempotencyKey, request, () =>
                {
                    ValidateMeasurementStart(actor.WorkspaceId, request);
                    DateTimeOffset now = clock.UtcNow;
                    string runId = NextId("pmr");
                    DateTimeOffset deadline = now.AddMinutes(30);
                    TenantControlJobRecord job = EnqueueTenantWorkCore(
                        actor.WorkspaceId,
                        ContractVersions.ProductMeasurementTimeout,
                        "ProductMeasurementRun",
                        JsonSerializer.Serialize(new { measurement_run_id = runId }, ContractVersions.JsonOptions),
                        JsonSerializer.Serialize(new
                        {
                            deadlineAt = deadline,
                            feature = request.Feature,
                            measurementRunSchemaVersion = ContractVersions.ProductMeasurementRun,
                            operation = "TERMINALIZE_AT_DEADLINE"
                        }, ContractVersions.JsonOptions),
                        $"measurement-run:{runId}:timeout");

                    ProductMeasurementRunRecord run = new(
                        runId,
                        actor.WorkspaceId,
                        request.Feature,
                        request.Mode,
                        request.PracticeIndex,
                        "OPEN",
                        now,
                        deadline,
                        null,
                        null,
                        job.TenantControlJobId);
                    _measurementRuns[runId] = run;
                    _measurementEvents[runId] = [new ProductMeasurementRunEventRecord(NextId("pmrevt"), runId, actor.WorkspaceId, 1, "START", now)];
                    return run;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ProductMeasurementRunRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ProductMeasurementRunRecord>> SucceedProductMeasurementAsync(
        ActorContext actor,
        CompleteProductMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "SucceedProductMeasurement", request.IdempotencyKey, request, () =>
                    TerminalizeMeasurement(actor, request.MeasurementRunId, "SUCCEEDED", null, "MEASUREMENT_RUN_SUCCEEDED")));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ProductMeasurementRunRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<CommandResult<ProductMeasurementRunRecord>> AbandonProductMeasurementAsync(
        ActorContext actor,
        AbandonProductMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, "AbandonProductMeasurement", request.IdempotencyKey, request, () =>
                    TerminalizeMeasurement(actor, request.MeasurementRunId, "ABANDONED", ValidateAbandonReason(request.Reason), "MEASUREMENT_RUN_ABANDONED")));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<ProductMeasurementRunRecord>.Fail(ex.Code));
            }
        }
    }

    public Task<int> TimeoutProductMeasurementsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            int count = 0;
            foreach (ProductMeasurementRunRecord run in _measurementRuns.Values.Where(r => r.WorkspaceId == actor.WorkspaceId && r.State == "OPEN" && clock.UtcNow >= r.DeadlineAt).ToList())
            {
                TerminalizeMeasurement(actor, run.MeasurementRunId, "ABANDONED", "TIMEOUT", "MEASUREMENT_RUN_ABANDONED");
                count++;
            }

            return Task.FromResult(count);
        }
    }

    public TenantControlJobRecord EnqueueTenantWorkForTest(ActorContext actor, string workType, string subjectType, object subject, object payload, string operationKey)
    {
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            return EnqueueTenantWorkCore(
                actor.WorkspaceId,
                workType,
                subjectType,
                JsonSerializer.Serialize(subject, ContractVersions.JsonOptions),
                JsonSerializer.Serialize(payload, ContractVersions.JsonOptions),
                operationKey);
        }
    }

    public TenantWorkItemTerminalMarkerRecord TerminalizeTenantWorkForTest(ActorContext actor, string jobId, string resultCode)
    {
        lock (_gate)
        {
            EnsureActorWorkspace(actor);
            return TerminalizeTenantWorkCore(jobId, resultCode);
        }
    }

    public ProviderDispatchPlan ResolveProvider(TenantControlJobRecord job) =>
        job.WorkType switch
        {
            ContractVersions.ProductMeasurementTimeout => new ProviderDispatchPlan(false, "internal:product-measurement-timeout"),
            ContractVersions.ObjectIngestFinalize => new ProviderDispatchPlan(true, "local:object-ingest-finalize"),
            ContractVersions.UploadValidate => new ProviderDispatchPlan(false, "internal:upload-validate"),
            ContractVersions.UploadPurge => new ProviderDispatchPlan(true, "local:upload-purge"),
            ContractVersions.Import => new ProviderDispatchPlan(false, "internal:import"),
            ContractVersions.Context => new ProviderDispatchPlan(false, "internal:context"),
            ContractVersions.AttachmentDelete => new ProviderDispatchPlan(true, "local:attachment-delete"),
            ContractVersions.Metrics => new ProviderDispatchPlan(false, "internal:metrics"),
            _ => throw new TradeProofException("UNREGISTERED_WORK_TYPE")
        };

    public IReadOnlyList<AuditEventRecord> AuditEvents
    {
        get
        {
            lock (_gate)
            {
                return _auditEvents.ToList();
            }
        }
    }

    public IReadOnlyList<TenantControlJobRecord> Jobs
    {
        get
        {
            lock (_gate)
            {
                return _jobs.Values.OrderBy(j => j.WorkSequence).ToList();
            }
        }
    }

    public IReadOnlyList<TenantWorkItemTerminalMarkerRecord> TerminalMarkers
    {
        get
        {
            lock (_gate)
            {
                return _markersByJob.Values.OrderBy(m => m.WorkSequence).ToList();
            }
        }
    }

    public ActorContext ActorFromBootstrap(BootstrapResponse bootstrap, ManagedIdentity identity) =>
        new(bootstrap.UserId, bootstrap.WorkspaceId, bootstrap.TradingAccountId, identity.Issuer, identity.Subject);

    private Task<CommandResult<SetupPresetRevisionRecord>> ToggleSetupPresetAsync(ActorContext actor, SetupPresetCommandRequest request, bool active)
    {
        lock (_gate)
        {
            try
            {
                EnsureActorWorkspace(actor);
                return Task.FromResult(RunIdempotent(actor.WorkspaceId, active ? "ReactivateSetupPreset" : "ArchiveSetupPreset", request.IdempotencyKey, request, () =>
                {
                    SetupPresetRevisionRecord current = CurrentSetupRevision(actor.WorkspaceId, request.SetupPresetId);
                    if (current.IsSystem)
                    {
                        throw new TradeProofException("SYSTEM_PRESET_IMMUTABLE");
                    }

                    SetupPresetRevisionRecord revision = current with
                    {
                        RevisionId = NextId("setuprev"),
                        RevisionNo = current.RevisionNo + 1,
                        IsActive = active,
                        RecordedAt = clock.UtcNow
                    };
                    _setupRevisions[request.SetupPresetId].Add(revision);
                    return revision;
                }));
            }
            catch (TradeProofException ex)
            {
                return Task.FromResult(CommandResult<SetupPresetRevisionRecord>.Fail(ex.Code));
            }
        }
    }

    private CommandResult<T> RunIdempotent<T>(string workspaceId, string commandType, string key, object request, Func<T> create)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new TradeProofException("IDEMPOTENCY_KEY_REQUIRED");
        }

        string receiptKey = $"{workspaceId}\u001F{commandType}\u001F{key}";
        string requestSha256 = ContractVersions.Sha256CanonicalJson(request);
        if (_idempotency.TryGetValue(receiptKey, out IdempotencyReceiptRecord? receipt))
        {
            if (!string.Equals(receipt.RequestSha256, requestSha256, StringComparison.Ordinal))
            {
                throw new TradeProofException("IDEMPOTENCY_CONFLICT");
            }

            T? replayed = JsonSerializer.Deserialize<T>(receipt.ResponseJson, ContractVersions.JsonOptions);
            return CommandResult<T>.Ok(replayed ?? throw new TradeProofException("IDEMPOTENCY_REPLAY_CORRUPT"));
        }

        T value = create();
        _idempotency[receiptKey] = new IdempotencyReceiptRecord(
            workspaceId,
            commandType,
            key,
            requestSha256,
            JsonSerializer.Serialize(value, ContractVersions.JsonOptions),
            clock.UtcNow);
        return CommandResult<T>.Ok(value);
    }

    private TenantControlJobRecord EnqueueTenantWorkCore(string workspaceId, string workType, string subjectType, string subjectKeyJson, string payloadJson, string operationKey)
    {
        if (!ContractVersions.RegisteredWorkTypes.Contains(workType, StringComparer.Ordinal))
        {
            throw new TradeProofException("UNREGISTERED_WORK_TYPE");
        }

        if (string.IsNullOrWhiteSpace(operationKey))
        {
            throw new TradeProofException("WORK_OPERATION_KEY_REQUIRED");
        }

        string payloadSha256 = ContractVersions.Sha256CanonicalJson(JsonSerializer.Deserialize<JsonElement>(payloadJson));
        TenantControlJobRecord? duplicate = _jobs.Values.FirstOrDefault(j =>
            j.WorkspaceId == workspaceId &&
            j.PayloadSchemaVersion == ContractVersions.TenantControlJobPayload &&
            j.WorkType == workType &&
            j.OperationIdempotencyKey == operationKey);
        if (duplicate is not null)
        {
            if (duplicate.PayloadSha256 != payloadSha256)
            {
                throw new TradeProofException("TENANT_CONTROL_JOB_IDEMPOTENCY_CONFLICT");
            }

            return duplicate;
        }

        TenantControlJobRecord? semanticDuplicate = _jobs.Values.FirstOrDefault(j =>
            j.WorkspaceId == workspaceId &&
            j.PayloadSchemaVersion == ContractVersions.TenantControlJobPayload &&
            j.WorkType == workType &&
            j.SubjectType == subjectType &&
            j.SubjectKeyJson == subjectKeyJson &&
            j.PayloadSha256 == payloadSha256);
        if (semanticDuplicate is not null)
        {
            return semanticDuplicate;
        }

        long sequence = _workSequences.TryGetValue(workspaceId, out long current) ? current + 1 : 1;
        _workSequences[workspaceId] = sequence;
        DateTimeOffset now = clock.UtcNow;
        string jobId = NextId("job");
        TenantControlJobRecord job = new(
            jobId,
            workspaceId,
            sequence,
            workType,
            subjectType,
            subjectKeyJson,
            ContractVersions.TenantControlJobPayload,
            "sha256:rfc8785-lite",
            payloadSha256,
            payloadJson,
            operationKey,
            _workspaces[workspaceId].DeletionGuardGeneration,
            "ENQUEUED",
            false,
            now);
        _jobs[jobId] = job;

        string fenceId = NextId("fence");
        TenantWorkItemFenceRecord fence = new(fenceId, jobId, workspaceId, sequence, "ACTIVE", now);
        _fencesByJob[jobId] = fence;
        _fenceEvents[fenceId] = [new TenantWorkItemFenceEventRecord(NextId("fenceevt"), fenceId, 1, "ENQUEUE", now)];
        return job;
    }

    private TenantWorkItemTerminalMarkerRecord TerminalizeTenantWorkCore(string jobId, string resultCode)
    {
        if (!_jobs.TryGetValue(jobId, out TenantControlJobRecord? job))
        {
            throw new TradeProofException("JOB_NOT_FOUND");
        }

        if (_markersByJob.TryGetValue(jobId, out TenantWorkItemTerminalMarkerRecord? marker))
        {
            return marker;
        }

        DateTimeOffset now = clock.UtcNow;
        marker = new TenantWorkItemTerminalMarkerRecord(
            NextId("marker"),
            job.TenantControlJobId,
            job.WorkspaceId,
            job.WorkSequence,
            job.WorkType,
            job.PayloadSchemaVersion,
            ContractVersions.TenantWorkItemTerminalMarker,
            job.PayloadDigestProfile,
            job.PayloadSha256,
            resultCode,
            now);
        _markersByJob[jobId] = marker;
        _jobs[jobId] = job with { State = "COMPLETED", Compacted = true, PayloadJson = null };
        TenantWorkItemFenceRecord fence = _fencesByJob[jobId];
        _fenceEvents[fence.TenantWorkItemFenceId].Add(new TenantWorkItemFenceEventRecord(
            NextId("fenceevt"),
            fence.TenantWorkItemFenceId,
            _fenceEvents[fence.TenantWorkItemFenceId].Count + 1,
            "COMPLETE",
            now));
        return marker;
    }

    private ProductMeasurementRunRecord TerminalizeMeasurement(ActorContext actor, string runId, string state, string? abandonReason, string resultCode)
    {
        if (!_measurementRuns.TryGetValue(runId, out ProductMeasurementRunRecord? run) || run.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("MEASUREMENT_RUN_NOT_FOUND");
        }

        if (run.State != "OPEN")
        {
            return run;
        }

        if (state == "SUCCEEDED" && clock.UtcNow >= run.DeadlineAt)
        {
            throw new TradeProofException("MEASUREMENT_RUN_DEADLINE_REACHED");
        }

        ProductMeasurementRunRecord terminal = run with
        {
            State = state,
            TerminalAt = clock.UtcNow,
            AbandonReason = abandonReason
        };
        _measurementRuns[runId] = terminal;
        _measurementEvents[runId].Add(new ProductMeasurementRunEventRecord(
            NextId("pmrevt"),
            runId,
            actor.WorkspaceId,
            2,
            state == "SUCCEEDED" ? "SUCCEED" : "ABANDON",
            clock.UtcNow));
        if (state == "ABANDONED")
        {
            RecordAnalytics(actor.WorkspaceId, "measurement_abandoned", new { measurement_run_id = runId }, new { reason = abandonReason }, clock.UtcNow);
        }

        TerminalizeTenantWorkCore(run.TimeoutTenantControlJobId, resultCode);
        return terminal;
    }

    private void ValidateMeasurementStart(string workspaceId, StartProductMeasurementRequest request)
    {
        string[] features = ["ONBOARDING", "QUICK_PLAN", "QUICK_REVIEW", "FIRST_INSIGHT"];
        if (!features.Contains(request.Feature, StringComparer.Ordinal))
        {
            throw new TradeProofException("MEASUREMENT_FEATURE_INVALID");
        }

        if (request.Mode is not ("PRACTICE" or "MEASURED"))
        {
            throw new TradeProofException("MEASUREMENT_MODE_INVALID");
        }

        if (request.Mode == "PRACTICE" && request.PracticeIndex is null)
        {
            throw new TradeProofException("MEASUREMENT_PRACTICE_SEQUENCE_INVALID");
        }

        if (request.Mode == "MEASURED" && request.PracticeIndex is not null)
        {
            throw new TradeProofException("MEASUREMENT_PRACTICE_SEQUENCE_INVALID");
        }

        if (_measurementRuns.Values.Any(r => r.WorkspaceId == workspaceId && r.Feature == request.Feature && r.State == "OPEN"))
        {
            throw new TradeProofException("MEASUREMENT_RUN_ALREADY_OPEN");
        }

        if (request.Feature == "ONBOARDING" && request.Mode == "PRACTICE")
        {
            throw new TradeProofException("ONBOARDING_PRACTICE_FORBIDDEN");
        }

        if (request.Feature == "QUICK_PLAN")
        {
            int terminalPracticeCount = _measurementRuns.Values.Count(r =>
                r.WorkspaceId == workspaceId &&
                r.Feature == "QUICK_PLAN" &&
                r.Mode == "PRACTICE" &&
                r.State is "SUCCEEDED" or "ABANDONED");
            bool measuredAlreadyStarted = _measurementRuns.Values.Any(r =>
                r.WorkspaceId == workspaceId &&
                r.Feature == "QUICK_PLAN" &&
                r.Mode == "MEASURED");

            if (request.Mode == "PRACTICE" && (measuredAlreadyStarted || terminalPracticeCount >= 3 || request.PracticeIndex != terminalPracticeCount + 1))
            {
                throw new TradeProofException("MEASUREMENT_PRACTICE_SEQUENCE_INVALID");
            }

            if (request.Mode == "MEASURED" && terminalPracticeCount != 3)
            {
                throw new TradeProofException("MEASUREMENT_REQUIRES_THREE_PRACTICES");
            }

            if (request.Mode == "MEASURED" && measuredAlreadyStarted)
            {
                throw new TradeProofException("MEASUREMENT_MEASURED_ALREADY_STARTED");
            }
        }
    }

    private static string ValidateAbandonReason(string reason)
    {
        string[] reasons =
        [
            "USER_CANCELLED",
            "NEGATIVE_DURATION",
            "ZERO_DURATION",
            "BACKGROUND_INTERRUPTED",
            "MISSING_TERMINAL_EVENT",
            "DURATION_OVER_30_MINUTES",
            "TIMEOUT"
        ];
        if (!reasons.Contains(reason, StringComparer.Ordinal))
        {
            throw new TradeProofException("MEASUREMENT_ABANDON_REASON_INVALID");
        }

        return reason;
    }

    private PlanFields ValidatePlanFields(ActorContext actor, ArmPlanRequest request)
    {
        SetupPresetRevisionRecord setup = _setupRevisions.Values.SelectMany(r => r)
            .SingleOrDefault(s => s.WorkspaceId == actor.WorkspaceId && s.RevisionId == request.SetupPresetRevisionId && s.IsActive)
            ?? throw new TradeProofException("SETUP_REVISION_NOT_FOUND");

        string symbol = request.Symbol.ToUpperInvariant();
        if (!symbol.EndsWith("USDT", StringComparison.Ordinal) || symbol.Length < 5 || !symbol.All(char.IsAsciiLetterOrDigit))
        {
            throw new TradeProofException("SYMBOL_UNSUPPORTED");
        }

        string low = ContractVersions.CanonicalizeDecimal(request.EntryZoneLow, 20, 18, true);
        string high = ContractVersions.CanonicalizeDecimal(request.EntryZoneHigh, 20, 18, true);
        string stop = ContractVersions.CanonicalizeDecimal(request.InitialStop, 20, 18, true);
        string risk = ContractVersions.CanonicalizeDecimal(request.PlannedRiskUsdt, 8, 8, true);
        decimal lowValue = decimal.Parse(low, System.Globalization.CultureInfo.InvariantCulture);
        decimal highValue = decimal.Parse(high, System.Globalization.CultureInfo.InvariantCulture);
        decimal stopValue = decimal.Parse(stop, System.Globalization.CultureInfo.InvariantCulture);
        if (lowValue > highValue || stopValue >= lowValue)
        {
            throw new TradeProofException("PLAN_PRICE_RELATION_INVALID");
        }

        if (request.Confidence is < 1 or > 5)
        {
            throw new TradeProofException("PLAN_CONFIDENCE_INVALID");
        }

        int expiry = request.ExpiryDurationSeconds ?? 86400;
        if (expiry is < 900 or > 604800)
        {
            throw new TradeProofException("PLAN_EXPIRY_INVALID");
        }

        string? thesis = string.IsNullOrWhiteSpace(request.Thesis) ? null : request.Thesis.Trim();
        if (thesis is { Length: > 1000 })
        {
            throw new TradeProofException("PLAN_THESIS_TOO_LONG");
        }

        return new PlanFields(symbol, setup.RevisionId, low, high, stop, risk, request.Confidence, thesis, setup.Checklist);
    }

    private void ValidateTradingAccount(ActorContext actor, string tradingAccountId)
    {
        if (!_accounts.TryGetValue(tradingAccountId, out TradingAccountRecord? account) || account.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("TRADING_ACCOUNT_NOT_FOUND");
        }
    }

    private TradePlanRevisionRecord CreatePlanRevision(string workspaceId, string planId, int revisionNo, PlanFields fields, DateTimeOffset submittedAt)
    {
        var hashBasis = new
        {
            checklist = fields.Checklist,
            confidence = fields.Confidence,
            entryZoneHigh = fields.EntryZoneHigh,
            entryZoneLow = fields.EntryZoneLow,
            initialStop = fields.InitialStop,
            plannedRiskUsdt = fields.PlannedRiskUsdt,
            setupPresetRevisionId = fields.SetupPresetRevisionId,
            thesis = fields.Thesis
        };
        return new TradePlanRevisionRecord(
            NextId("planrev"),
            planId,
            workspaceId,
            revisionNo,
            fields.SetupPresetRevisionId,
            fields.EntryZoneLow,
            fields.EntryZoneHigh,
            fields.InitialStop,
            fields.PlannedRiskUsdt,
            fields.Confidence,
            fields.Thesis,
            fields.Checklist,
            submittedAt,
            ContractVersions.Sha256CanonicalJson(hashBasis));
    }

    private void AddPlanEvent(string planId, string workspaceId, string eventType, DateTimeOffset recordedAt)
    {
        List<TradePlanEventRecord> events = _planEvents[planId];
        events.Add(new TradePlanEventRecord(NextId("planevt"), planId, workspaceId, events.Count + 1, eventType, recordedAt));
    }

    private TradePlanHeaderRecord GetOwnedPlan(ActorContext actor, string planId)
    {
        if (!_plans.TryGetValue(planId, out TradePlanHeaderRecord? plan) || plan.WorkspaceId != actor.WorkspaceId)
        {
            throw new TradeProofException("PLAN_NOT_FOUND");
        }

        return plan;
    }

    private static IReadOnlyList<ChecklistItemRecord> BuildChecklist(IReadOnlyList<ChecklistItemInput> inputs)
    {
        if (inputs.Count > 10)
        {
            throw new TradeProofException("CHECKLIST_TOO_LONG");
        }

        return inputs.Select((item, index) =>
        {
            string label = item.Label.Trim();
            if (label.Length is < 1 or > 120)
            {
                throw new TradeProofException("CHECKLIST_LABEL_INVALID");
            }

            return new ChecklistItemRecord($"chk_{index + 1:D2}", label, item.Required);
        }).ToList();
    }

    private SetupPresetRevisionRecord CurrentSetupRevision(string workspaceId, string setupPresetId)
    {
        if (!_setupRevisions.TryGetValue(setupPresetId, out List<SetupPresetRevisionRecord>? revisions))
        {
            throw new TradeProofException("SETUP_NOT_FOUND");
        }

        SetupPresetRevisionRecord current = revisions.MaxBy(r => r.RevisionNo) ?? throw new TradeProofException("SETUP_NOT_FOUND");
        if (current.WorkspaceId != workspaceId)
        {
            throw new TradeProofException("SETUP_NOT_FOUND");
        }

        return current;
    }

    private IReadOnlyList<SetupPresetRevisionRecord> ActiveSetupPresets(string workspaceId) =>
        _setupRevisions.Values
            .Select(revisions => revisions.MaxBy(r => r.RevisionNo))
            .OfType<SetupPresetRevisionRecord>()
            .Where(r => r.WorkspaceId == workspaceId && r.IsActive)
            .OrderBy(r => r.IsSystem ? 0 : 1)
            .ThenBy(r => r.Label, StringComparer.Ordinal)
            .ToList();

    private void CreateSystemOtherPreset(string workspaceId, DateTimeOffset now)
    {
        string presetId = NextId("setup");
        SetupPresetRevisionRecord revision = new(
            presetId,
            NextId("setuprev"),
            workspaceId,
            1,
            ContractVersions.SetupPreset,
            "OTHER",
            "OTHER",
            [],
            true,
            true,
            now);
        _setupRevisions[presetId] = [revision];
    }

    private BootstrapResponse BuildBootstrap(string userId)
    {
        WorkspaceRecord workspace = _workspaces.Values.Single(w => w.OwnerUserId == userId);
        TradingAccountRecord account = _accounts.Values.Single(a => a.WorkspaceId == workspace.WorkspaceId);
        return new BootstrapResponse(userId, workspace.WorkspaceId, account.TradingAccountId, workspace.Timezone, ActiveSetupPresets(workspace.WorkspaceId));
    }

    private void EnsureActorWorkspace(ActorContext actor)
    {
        WorkspaceRecord workspace = _workspaces.GetValueOrDefault(actor.WorkspaceId) ?? throw new TradeProofException("WORKSPACE_NOT_FOUND");
        if (workspace.OwnerUserId != actor.ActorUserId || workspace.LifecycleState != "ACTIVE")
        {
            throw new TradeProofException("WORKSPACE_ACCESS_DENIED");
        }
    }

    private void RecordAudit(string branch, string eventType, string? workspaceId, string? actorUserId, string safeCode)
    {
        _auditEvents.Add(new AuditEventRecord(NextId("audit"), branch, eventType, workspaceId, actorUserId, safeCode, clock.UtcNow));
    }

    private void RecordAnalytics(string workspaceId, string eventType, object sourceRecordKey, object payload, DateTimeOffset recordedAt)
    {
        _analyticsEvents.Add(new ProductAnalyticsEventRecord(
            NextId("ana"),
            workspaceId,
            eventType,
            JsonSerializer.Serialize(sourceRecordKey, ContractVersions.JsonOptions),
            JsonSerializer.Serialize(payload, ContractVersions.JsonOptions),
            recordedAt));
    }

    private string NextId(string prefix) => $"{prefix}_{++_id:D8}";

    private sealed record PlanFields(
        string Symbol,
        string SetupPresetRevisionId,
        string EntryZoneLow,
        string EntryZoneHigh,
        string InitialStop,
        string PlannedRiskUsdt,
        int Confidence,
        string? Thesis,
        IReadOnlyList<ChecklistItemRecord> Checklist);
}
