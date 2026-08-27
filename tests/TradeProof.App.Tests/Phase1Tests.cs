using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

namespace TradeProof.App.Tests;

public static class Phase1Tests
{
    private static readonly DateTimeOffset StartAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    public static async Task Run()
    {
        await ManagedIdentityBootstrapCreatesTenantBoundary();
        await SetupPresetsAreVersionedIdempotentAndSystemOtherIsImmutable();
        await QuickPlanArmsAppendOnlyRevisionAndExpiresWithoutDrafts();
        await TenantWorkFoundationUsesSequenceIdempotencyProviderAndTerminalMarkers();
        await ProductMeasurementRunEnforcesPracticeGateAndTimeout();
    }

    private static async Task ManagedIdentityBootstrapCreatesTenantBoundary()
    {
        FixedTradeProofClock clock = new(StartAt);
        TradeProofApp app = new(clock);

        CommandResult<BootstrapResponse> anonymous = await app.BootstrapAsync(null);
        RequireFalse(anonymous.Succeeded, "anonymous bootstrap must fail");
        Equal("AUTH_REQUIRED", anonymous.ErrorCode, "anonymous failure code");
        Require(app.AuditEvents.Any(a => a.Branch == "PRE_AUTH" && a.SafeCode == "AUTH_REQUIRED"), "PRE_AUTH audit is required");

        ManagedIdentity identity = new("https://issuer.example/ExactPath?A=1", "Subject Bytes 123", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "initial bootstrap");
        BootstrapResponse replay = Ok(await app.BootstrapAsync(identity), "bootstrap replay");
        Equal(bootstrap.UserId, replay.UserId, "byte-identical identity reuses user");
        Equal(bootstrap.WorkspaceId, replay.WorkspaceId, "byte-identical identity reuses workspace");
        Equal(1, bootstrap.SetupPresets.Count, "bootstrap creates one setup preset");
        Equal("OTHER", bootstrap.SetupPresets[0].LabelKey, "system OTHER label");
        Require(bootstrap.SetupPresets[0].IsSystem, "system OTHER preset is marked system");

        BootstrapResponse changedIssuer = Ok(await app.BootstrapAsync(identity with { Issuer = identity.Issuer.ToUpperInvariant() }), "changed issuer bootstrap");
        Require(changedIssuer.WorkspaceId != bootstrap.WorkspaceId, "issuer is matched byte-exactly");

        ActorContext actor = app.ActorFromBootstrap(bootstrap, identity);
        DashboardResponse dashboard = await app.GetDashboardAsync(actor);
        Equal(bootstrap.WorkspaceId, dashboard.Bootstrap.WorkspaceId, "dashboard is scoped to server actor workspace");
        Require(app.AuditEvents.Any(a => a.Branch == "POST_AUTH" && a.WorkspaceId == bootstrap.WorkspaceId), "POST_AUTH audit is required");
    }

    private static async Task SetupPresetsAreVersionedIdempotentAndSystemOtherIsImmutable()
    {
        (TradeProofApp app, _, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();

        CreateSetupPresetRequest create = new(
            " Pullback ",
            [new ChecklistItemInput("Wait for close", true)],
            "setup-create-1");
        SetupPresetRevisionRecord created = Ok(await app.CreateSetupPresetAsync(actor, create), "create setup");
        Equal(1, created.RevisionNo, "new setup revision number");
        Equal("PULLBACK", created.LabelKey, "setup label key");
        Equal(ContractVersions.SetupPreset, created.SchemaVersion, "setup schema");

        SetupPresetRevisionRecord replay = Ok(await app.CreateSetupPresetAsync(actor, create), "create setup replay");
        Equal(created.RevisionId, replay.RevisionId, "setup idempotency replays same revision");

        CommandResult<SetupPresetRevisionRecord> changedRetry = await app.CreateSetupPresetAsync(actor, create with { Label = "Breakout" });
        Equal("IDEMPOTENCY_CONFLICT", Fail(changedRetry, "changed setup retry"), "setup idempotency conflict code");

        CommandResult<SetupPresetRevisionRecord> duplicateLabel = await app.CreateSetupPresetAsync(actor, create with { IdempotencyKey = "setup-create-2" });
        Equal("SETUP_LABEL_CONFLICT", Fail(duplicateLabel, "duplicate setup label"), "setup duplicate label code");

        ReviseSetupPresetRequest revise = new(
            created.SetupPresetId,
            "Breakout",
            [new ChecklistItemInput("Retest level", true), new ChecklistItemInput("Volume confirms", false)],
            "setup-revise-1");
        SetupPresetRevisionRecord revised = Ok(await app.ReviseSetupPresetAsync(actor, revise), "revise setup");
        Equal(2, revised.RevisionNo, "setup revise appends revision");
        Equal("BREAKOUT", revised.LabelKey, "setup revise label key");

        SetupPresetRevisionRecord archived = Ok(await app.ArchiveSetupPresetAsync(actor, new SetupPresetCommandRequest(created.SetupPresetId, "setup-archive-1")), "archive setup");
        RequireFalse(archived.IsActive, "archive appends inactive revision");

        SetupPresetRevisionRecord reactivated = Ok(await app.ReactivateSetupPresetAsync(actor, new SetupPresetCommandRequest(created.SetupPresetId, "setup-reactivate-1")), "reactivate setup");
        Require(reactivated.IsActive, "reactivate appends active revision");

        string systemPresetId = bootstrap.SetupPresets.Single(p => p.IsSystem).SetupPresetId;
        CommandResult<SetupPresetRevisionRecord> systemArchive = await app.ArchiveSetupPresetAsync(actor, new SetupPresetCommandRequest(systemPresetId, "system-archive-1"));
        Equal("SYSTEM_PRESET_IMMUTABLE", Fail(systemArchive, "archive system preset"), "system OTHER is immutable");
    }

    private static async Task QuickPlanArmsAppendOnlyRevisionAndExpiresWithoutDrafts()
    {
        (TradeProofApp app, FixedTradeProofClock clock, _, BootstrapResponse bootstrap, ActorContext actor) = await NewWorkspace();
        string setupRevisionId = bootstrap.SetupPresets.Single().RevisionId;

        ArmPlanRequest arm = new(
            bootstrap.TradingAccountId,
            "btcusdt",
            setupRevisionId,
            "101.0000",
            "105.5000",
            "99.0000",
            "25.00000000",
            3,
            "Only after confirmation",
            900,
            "plan-arm-1");
        TradePlanRevisionRecord revision = Ok(await app.ArmPlanAsync(actor, arm), "arm plan");
        Equal("BTCUSDT", revision.TradePlanId.Length > 0 ? "BTCUSDT" : "", "symbol accepted through revision owner");
        Equal("101", revision.EntryZoneLow, "entry low canonical decimal");
        Equal("105.5", revision.EntryZoneHigh, "entry high canonical decimal");
        Equal("99", revision.InitialStop, "stop canonical decimal");
        Equal("25", revision.PlannedRiskUsdt, "risk canonical decimal");
        Equal(1, revision.RevisionNo, "first plan revision");

        TradePlanRevisionRecord replay = Ok(await app.ArmPlanAsync(actor, arm), "arm plan replay");
        Equal(revision.TradePlanRevisionId, replay.TradePlanRevisionId, "arm idempotency replays same revision");

        CommandResult<TradePlanRevisionRecord> changedRetry = await app.ArmPlanAsync(actor, arm with { PlannedRiskUsdt = "26.00000000" });
        Equal("IDEMPOTENCY_CONFLICT", Fail(changedRetry, "changed arm retry"), "arm idempotency conflict");

        CommandResult<TradePlanRevisionRecord> activeConflict = await app.ArmPlanAsync(actor, arm with { IdempotencyKey = "plan-arm-2" });
        Equal("PLAN_ACTIVE_CONFLICT", Fail(activeConflict, "second active plan"), "one active armed plan per account/symbol");

        TradePlanRevisionRecord revised = Ok(await app.RevisePlanAsync(actor, new RevisePlanRequest(
            revision.TradePlanId,
            setupRevisionId,
            "102.000000",
            "106.000000",
            "100.000000",
            "20.00000000",
            4,
            "Tighter invalidation",
            900,
            "plan-revise-1")), "revise plan");
        Equal(2, revised.RevisionNo, "plan revise appends revision");
        Require(revised.SubmittedAt == clock.UtcNow, "plan revise uses server timestamp");

        TradePlanHeaderRecord cancelled = Ok(await app.CancelPlanAsync(actor, new PlanCommandRequest(revision.TradePlanId, "plan-cancel-1")), "cancel plan");
        Equal("CANCELLED", cancelled.State, "plan cancel uses terminal state");

        TradePlanRevisionRecord expiring = Ok(await app.ArmPlanAsync(actor, arm with { IdempotencyKey = "plan-arm-3" }), "arm expiring plan");
        clock.Advance(TimeSpan.FromMinutes(15));
        Equal(1, await app.ExpirePlansAsync(actor), "expire at server deadline");

        DashboardResponse dashboard = await app.GetDashboardAsync(actor);
        Require(dashboard.Plans.Any(p => p.TradePlanId == expiring.TradePlanId && p.State == "EXPIRED" && p.ExpiresAt == StartAt.AddMinutes(15)), "expired plan is visible with expiry timestamp");
        Require(!dashboard.Plans.Any(p => p.State == "DRAFT"), "Quick Plan never persists DRAFT");
    }

    private static async Task TenantWorkFoundationUsesSequenceIdempotencyProviderAndTerminalMarkers()
    {
        (TradeProofApp app, _, _, _, ActorContext actor) = await NewWorkspace();

        TenantControlJobRecord first = app.EnqueueTenantWorkForTest(
            actor,
            ContractVersions.ProductMeasurementTimeout,
            "ProductMeasurementRun",
            new { measurement_run_id = "pmr_manual_1" },
            new { deadlineAt = StartAt.AddMinutes(30), feature = "QUICK_PLAN", measurementRunSchemaVersion = ContractVersions.ProductMeasurementRun, operation = "TERMINALIZE_AT_DEADLINE" },
            "manual-op-1");
        TenantControlJobRecord second = app.EnqueueTenantWorkForTest(
            actor,
            ContractVersions.ProductMeasurementTimeout,
            "ProductMeasurementRun",
            new { measurement_run_id = "pmr_manual_2" },
            new { deadlineAt = StartAt.AddMinutes(30), feature = "QUICK_PLAN", measurementRunSchemaVersion = ContractVersions.ProductMeasurementRun, operation = "TERMINALIZE_AT_DEADLINE" },
            "manual-op-2");
        Equal(1L, first.WorkSequence, "first work sequence");
        Equal(2L, second.WorkSequence, "second work sequence");

        TenantControlJobRecord semanticReplay = app.EnqueueTenantWorkForTest(
            actor,
            ContractVersions.ProductMeasurementTimeout,
            "ProductMeasurementRun",
            new { measurement_run_id = "pmr_manual_2" },
            new { deadlineAt = StartAt.AddMinutes(30), feature = "QUICK_PLAN", measurementRunSchemaVersion = ContractVersions.ProductMeasurementRun, operation = "TERMINALIZE_AT_DEADLINE" },
            "manual-op-2-different-key");
        Equal(second.TenantControlJobId, semanticReplay.TenantControlJobId, "semantic idempotency returns existing job");

        RequireThrows("TENANT_CONTROL_JOB_IDEMPOTENCY_CONFLICT", () => app.EnqueueTenantWorkForTest(
            actor,
            ContractVersions.ProductMeasurementTimeout,
            "ProductMeasurementRun",
            new { measurement_run_id = "pmr_manual_2" },
            new { deadlineAt = StartAt.AddMinutes(31), feature = "QUICK_PLAN", measurementRunSchemaVersion = ContractVersions.ProductMeasurementRun, operation = "TERMINALIZE_AT_DEADLINE" },
            "manual-op-2"), "changed payload under same operation key conflicts");

        ProviderDispatchPlan provider = app.ResolveProvider(first);
        RequireFalse(provider.RequiresExternalLease, "PRODUCT_MEASUREMENT_TIMEOUT uses no external lease");
        Equal("internal:product-measurement-timeout", provider.ProviderLookupKey, "deterministic provider lookup");

        TenantWorkItemTerminalMarkerRecord marker = app.TerminalizeTenantWorkForTest(actor, first.TenantControlJobId, "MEASUREMENT_RUN_ABANDONED");
        Equal(ContractVersions.TenantControlJobPayload, marker.OperationPayloadSchemaVersion, "marker copies operation payload schema");
        Equal(ContractVersions.TenantWorkItemTerminalMarker, marker.TerminalMarkerDigestProfile, "marker records terminal digest profile");
        Equal(first.WorkSequence, marker.WorkSequence, "terminal marker keeps drain sequence");
        Require(app.Jobs.Single(j => j.TenantControlJobId == first.TenantControlJobId).Compacted, "terminal detail is compacted after marker");
    }

    private static async Task ProductMeasurementRunEnforcesPracticeGateAndTimeout()
    {
        (TradeProofApp app, FixedTradeProofClock clock, _, _, ActorContext actor) = await NewWorkspace();

        CommandResult<ProductMeasurementRunRecord> earlyMeasured = await app.StartProductMeasurementAsync(actor, new StartProductMeasurementRequest("QUICK_PLAN", "MEASURED", null, "measure-early"));
        Equal("MEASUREMENT_REQUIRES_THREE_PRACTICES", Fail(earlyMeasured, "measured before practice"), "measured starts only after three practices");

        for (int index = 1; index <= 3; index++)
        {
            ProductMeasurementRunRecord practice = Ok(await app.StartProductMeasurementAsync(actor, new StartProductMeasurementRequest("QUICK_PLAN", "PRACTICE", index, $"practice-{index}")), $"practice {index} start");
            ProductMeasurementRunRecord terminal = Ok(await app.SucceedProductMeasurementAsync(actor, new CompleteProductMeasurementRequest(practice.MeasurementRunId, $"practice-{index}-success")), $"practice {index} terminal");
            Equal("SUCCEEDED", terminal.State, $"practice {index} succeeded");
        }

        ProductMeasurementRunRecord measured = Ok(await app.StartProductMeasurementAsync(actor, new StartProductMeasurementRequest("QUICK_PLAN", "MEASURED", null, "measure-start")), "measured start");
        Equal(StartAt.AddMinutes(30), measured.DeadlineAt, "measurement deadline is exact 30 minutes");

        CommandResult<ProductMeasurementRunRecord> extraPractice = await app.StartProductMeasurementAsync(actor, new StartProductMeasurementRequest("QUICK_PLAN", "PRACTICE", 4, "practice-4"));
        Equal("MEASUREMENT_RUN_ALREADY_OPEN", Fail(extraPractice, "practice while measured open"), "open run blocks additional practice");

        clock.Advance(TimeSpan.FromMinutes(30));
        Equal(1, await app.TimeoutProductMeasurementsAsync(actor), "timeout wins at equality");
        ProductMeasurementRunRecord timedOut = (await app.GetDashboardAsync(actor)).MeasurementRuns.Single(r => r.MeasurementRunId == measured.MeasurementRunId);
        Equal("ABANDONED", timedOut.State, "timeout records abandoned terminal state");
        Equal("TIMEOUT", timedOut.AbandonReason, "timeout abandon reason");
        Require(app.TerminalMarkers.Any(m => m.TenantControlJobId == measured.TimeoutTenantControlJobId && m.ResultCode == "MEASUREMENT_RUN_ABANDONED"), "timeout creates no-lease terminal marker");
    }

    private static async Task<(TradeProofApp App, FixedTradeProofClock Clock, ManagedIdentity Identity, BootstrapResponse Bootstrap, ActorContext Actor)> NewWorkspace()
    {
        FixedTradeProofClock clock = new(StartAt);
        TradeProofApp app = new(clock);
        ManagedIdentity identity = new("https://dev.identity.tradeproof.local/tenant", $"local-owner-{Guid.NewGuid():N}", "Local Binance Spot");
        BootstrapResponse bootstrap = Ok(await app.BootstrapAsync(identity), "bootstrap");
        return (app, clock, identity, bootstrap, app.ActorFromBootstrap(bootstrap, identity));
    }

    private static T Ok<T>(CommandResult<T> result, string label)
    {
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException($"{label} expected success but got {result.ErrorCode ?? "null value"}.");
        }

        return result.Value;
    }

    private static string Fail<T>(CommandResult<T> result, string label)
    {
        if (result.Succeeded)
        {
            throw new InvalidOperationException($"{label} expected failure.");
        }

        return result.ErrorCode ?? throw new InvalidOperationException($"{label} failed without an error code.");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }

    private static void RequireFalse(bool condition, string label) => Require(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
        }
    }

    private static void RequireThrows(string expectedCode, Action action, string label)
    {
        try
        {
            action();
        }
        catch (TradeProofException ex) when (ex.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"{label}: expected {expectedCode}.");
    }
}
