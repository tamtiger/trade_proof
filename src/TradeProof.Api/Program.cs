using System.Text;
using TradeProof.Application.Foundation;
using TradeProof.Domain.Foundation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<ITradeProofClock, SystemTradeProofClock>();
builder.Services.AddSingleton<TradeProofApp>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "TradeProof.Api",
    phase = "phase-6"
}));

app.MapGet("/openapi.json", () => Results.Ok(new
{
    openapi = "3.1.0",
    info = new { title = "TradeProof API", version = "phase-6" },
    paths = new[]
    {
        "/api/bootstrap",
        "/api/setup-presets",
        "/api/plans/arm",
        "/api/product-measurements/start",
        "/api/imports/reserve",
        "/api/imports/{objectIngestReservationId}/record-bytes",
        "/api/imports/{objectIngestReservationId}/transfer",
        "/api/uploads/{uploadId}/validate",
        "/api/uploads/{uploadId}/purge",
        "/api/attachments/reserve",
        "/api/attachments/{uploadId}/validate",
        "/api/attachments/{attachmentId}/delete",
        "/api/imports/confirm",
        "/api/imports/{importBatchId}/process",
        "/api/imports/{importBatchId}/progress",
        "/api/market/conversion-catalog",
        "/api/market/bars",
        "/api/context/compute",
        "/api/context/manual-recompute",
        "/api/reviews/complete",
        "/api/reviews/{reviewId}/revise",
        "/api/metrics/publish",
        "/api/weekly-lab/publish",
        "/api/weekly-lab/experiments/propose",
        "/api/weekly-lab/experiments/{behavioralExperimentId}/confirm",
        "/api/weekly-lab/experiments/{behavioralExperimentId}/cancel",
        "/api/weekly-lab/complete",
        "/api/product-analytics/events",
        "/api/product-metrics/workspace/publish",
        "/api/product-metrics/internal/publish",
        "/api/product-analytics/external/project",
        "/api/product-analytics/external/{externalAnalyticsProjectionId}/purge",
        "/api/exports/request",
        "/api/exports/{tradeProofExportId}/round-trip",
        "/api/exports/{tradeProofExportId}/expire",
        "/api/workspace/delete-request",
        "/api/workspace/deletions/{workspaceDeletionId}/complete"
    }
}));

var api = app.MapGroup("/api").WithTags("TradeProof");

api.MapGet("/bootstrap", async (HttpContext http, TradeProofApp tradeProof, CancellationToken ct) =>
{
    ManagedIdentity? identity = ReadManagedIdentity(http);
    CommandResult<BootstrapResponse> result = await tradeProof.BootstrapAsync(identity, ct);
    return result.Succeeded ? Results.Ok(result.Value) : Results.Unauthorized();
});

api.MapGet("/dashboard", async (HttpContext http, TradeProofApp tradeProof, CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    if (!actor.Succeeded)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await tradeProof.GetDashboardAsync(actor.Value!, ct));
});

api.MapPost("/market/conversion-catalog", async (
    PublishMarketConversionCatalogRequest request,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    return ToHttp(await tradeProof.PublishMarketConversionCatalogAsync(request, ct));
});

api.MapPost("/market/bars", async (
    RecordMarketBarsRequest request,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    return ToHttp(await tradeProof.RecordMarketBarsAsync(request, ct));
});

api.MapPost("/setup-presets", async (
    CreateSetupPresetRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.CreateSetupPresetAsync(actor.Value!, request, ct))
        : Results.Unauthorized();
});

api.MapPost("/setup-presets/{setupPresetId}/revise", async (
    string setupPresetId,
    ReviseSetupPresetApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ReviseSetupPresetAsync(actor.Value!, new ReviseSetupPresetRequest(setupPresetId, request.Label, request.Checklist, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/setup-presets/{setupPresetId}/archive", async (
    string setupPresetId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ArchiveSetupPresetAsync(actor.Value!, new SetupPresetCommandRequest(setupPresetId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/setup-presets/{setupPresetId}/reactivate", async (
    string setupPresetId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ReactivateSetupPresetAsync(actor.Value!, new SetupPresetCommandRequest(setupPresetId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/plans/arm", async (
    ArmPlanRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ArmPlanAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/plans/{tradePlanId}/revise", async (
    string tradePlanId,
    RevisePlanApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.RevisePlanAsync(actor.Value!, new RevisePlanRequest(
            tradePlanId,
            request.SetupPresetRevisionId,
            request.EntryZoneLow,
            request.EntryZoneHigh,
            request.InitialStop,
            request.PlannedRiskUsdt,
            request.Confidence,
            request.Thesis,
            request.ExpiryDurationSeconds,
            request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/plans/{tradePlanId}/cancel", async (
    string tradePlanId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.CancelPlanAsync(actor.Value!, new PlanCommandRequest(tradePlanId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/plans/expire", async (HttpContext http, TradeProofApp tradeProof, CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? Results.Ok(new { expired = await tradeProof.ExpirePlansAsync(actor.Value!, ct) })
        : Results.Unauthorized();
});

api.MapPost("/product-measurements/start", async (
    StartProductMeasurementRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.StartProductMeasurementAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/product-measurements/{measurementRunId}/succeed", async (
    string measurementRunId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.SucceedProductMeasurementAsync(actor.Value!, new CompleteProductMeasurementRequest(measurementRunId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/product-measurements/{measurementRunId}/abandon", async (
    string measurementRunId,
    AbandonMeasurementApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.AbandonProductMeasurementAsync(actor.Value!, new AbandonProductMeasurementRequest(measurementRunId, request.Reason, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/product-measurements/timeout", async (HttpContext http, TradeProofApp tradeProof, CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? Results.Ok(new { terminalized = await tradeProof.TimeoutProductMeasurementsAsync(actor.Value!, ct) })
        : Results.Unauthorized();
});

api.MapPost("/imports/reserve", async (
    ReserveRawUploadRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ReserveRawUploadAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/imports/{objectIngestReservationId}/record-bytes", async (
    string objectIngestReservationId,
    RecordUploadBytesApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.RecordReservedBytesAsync(actor.Value!, new RecordReservedBytesRequest(
            objectIngestReservationId,
            request.WriteCapabilityId,
            DecodeUploadBytes(request),
            request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/imports/{objectIngestReservationId}/transfer", async (
    string objectIngestReservationId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.TransferRawUploadAsync(actor.Value!, new TransferRawUploadRequest(objectIngestReservationId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/uploads/{uploadId}/validate", async (
    string uploadId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ValidateUploadAsync(actor.Value!, new ValidateUploadRequest(uploadId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/uploads/{uploadId}/purge", async (
    string uploadId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.PurgeUploadAsync(actor.Value!, new PurgeUploadRequest(uploadId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/attachments/reserve", async (
    ReserveReviewAttachmentRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ReserveReviewAttachmentAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/attachments/{uploadId}/validate", async (
    string uploadId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ValidateAttachmentUploadAsync(actor.Value!, new ValidateAttachmentUploadRequest(uploadId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/attachments/{attachmentId}/delete", async (
    string attachmentId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.DeleteAttachmentAsync(actor.Value!, new DeleteAttachmentRequest(attachmentId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/object-ingest/finalize", async (HttpContext http, TradeProofApp tradeProof, CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? Results.Ok(new { finalized = await tradeProof.FinalizeObjectIngestReservationsAsync(actor.Value!, ct) })
        : Results.Unauthorized();
});

api.MapPost("/imports/confirm", async (
    ConfirmImportRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ConfirmImportAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/imports/{importBatchId}/process", async (
    string importBatchId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ProcessImportAsync(actor.Value!, new ProcessImportRequest(importBatchId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapGet("/imports/{importBatchId}/progress", async (
    string importBatchId,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.GetImportProgressAsync(actor.Value!, importBatchId, ct))
        : Results.Unauthorized();
});

api.MapPost("/context/compute", async (
    ComputeContextSnapshotsRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ComputeContextSnapshotsAsync(actor.Value!, request, ct))
        : Results.Unauthorized();
});

api.MapPost("/context/manual-recompute", async (
    RequestManualContextRecomputeRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.RequestManualContextRecomputeAsync(actor.Value!, request, ct))
        : Results.Unauthorized();
});

api.MapPost("/reviews/complete", async (
    CompleteEpisodeReviewRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.CompleteEpisodeReviewAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/reviews/{reviewId}/revise", async (
    string reviewId,
    ReviseEpisodeReviewApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ReviseEpisodeReviewAsync(actor.Value!, new ReviseEpisodeReviewRequest(
            reviewId,
            request.ExpectedEpisodeProjectionVersion,
            request.ExpectedRevisionNo,
            request.ExitReason,
            request.ExitReasonOtherText,
            request.RuleBreach,
            request.BreachTypeIds,
            request.BreachOtherText,
            request.StopMovedAway,
            request.RiskExceeded,
            request.RequiredChecklistResults,
            request.Emotion,
            request.Lesson,
            request.AttachmentId,
            request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/metrics/publish", async (
    PublishMetricSnapshotsRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.PublishMetricSnapshotsAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/weekly-lab/publish", async (
    PublishWeeklyLabRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.PublishWeeklyLabAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/weekly-lab/experiments/propose", async (
    ProposeBehavioralExperimentRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ProposeBehavioralExperimentAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/weekly-lab/experiments/{behavioralExperimentId}/confirm", async (
    string behavioralExperimentId,
    ConfirmBehavioralExperimentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ConfirmBehavioralExperimentAsync(actor.Value!, new ConfirmBehavioralExperimentRequest(
            behavioralExperimentId,
            request.ExpectedRevisionNo,
            request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/weekly-lab/experiments/{behavioralExperimentId}/cancel", async (
    string behavioralExperimentId,
    CancelBehavioralExperimentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.CancelBehavioralExperimentAsync(actor.Value!, new CancelBehavioralExperimentRequest(
            behavioralExperimentId,
            request.ExpectedRevisionNo,
            request.Reason,
            request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/weekly-lab/complete", async (
    CompleteWeeklyReviewRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.CompleteWeeklyReviewAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/product-analytics/events", async (
    RecordProductAnalyticsEventRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.RecordProductAnalyticsEventAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/product-metrics/workspace/publish", async (
    PublishWorkspaceProductMetricsRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.PublishWorkspaceProductMetricsAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/product-metrics/internal/publish", async (
    PublishInternalAggregateProductMetricRequest request,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    return ToHttp(await tradeProof.PublishInternalAggregateProductMetricAsync(request, ct));
});

api.MapPost("/product-analytics/external/project", async (
    ProjectExternalAnalyticsRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.ProjectExternalAnalyticsAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/product-analytics/external/{externalAnalyticsProjectionId}/purge", async (
    string externalAnalyticsProjectionId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.PurgeExternalAnalyticsAsync(actor.Value!, new PurgeExternalAnalyticsRequest(externalAnalyticsProjectionId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/exports/request", async (
    RequestTradeProofExportRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded ? ToHttp(await tradeProof.RequestTradeProofExportAsync(actor.Value!, request, ct)) : Results.Unauthorized();
});

api.MapPost("/exports/{tradeProofExportId}/round-trip", async (
    string tradeProofExportId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ValidateExportRoundTripAsync(actor.Value!, new ValidateExportRoundTripRequest(tradeProofExportId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/exports/{tradeProofExportId}/expire", async (
    string tradeProofExportId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.ExpireExportAsync(actor.Value!, new ExpireExportRequest(tradeProofExportId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/workspace/delete-request", async (
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.RequestWorkspaceDeletionAsync(actor.Value!, new RequestWorkspaceDeletionRequest(request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

api.MapPost("/workspace/deletions/{workspaceDeletionId}/complete", async (
    string workspaceDeletionId,
    IdempotentApiRequest request,
    HttpContext http,
    TradeProofApp tradeProof,
    CancellationToken ct) =>
{
    CommandResult<ActorContext> actor = await ResolveActorAsync(http, tradeProof, ct);
    return actor.Succeeded
        ? ToHttp(await tradeProof.CompleteWorkspaceDeletionAsync(actor.Value!, new CompleteWorkspaceDeletionRequest(workspaceDeletionId, request.IdempotencyKey), ct))
        : Results.Unauthorized();
});

app.Run();

static ManagedIdentity? ReadManagedIdentity(HttpContext http)
{
    string? issuer = http.Request.Headers["X-TradeProof-Issuer"].FirstOrDefault();
    string? subject = http.Request.Headers["X-TradeProof-Subject"].FirstOrDefault();
    string? displayName = http.Request.Headers["X-TradeProof-Display-Name"].FirstOrDefault();
    return issuer is null || subject is null ? null : new ManagedIdentity(issuer, subject, displayName);
}

static async Task<CommandResult<ActorContext>> ResolveActorAsync(HttpContext http, TradeProofApp tradeProof, CancellationToken ct)
{
    ManagedIdentity? identity = ReadManagedIdentity(http);
    CommandResult<BootstrapResponse> bootstrap = await tradeProof.BootstrapAsync(identity, ct);
    return bootstrap.Succeeded
        ? CommandResult<ActorContext>.Ok(tradeProof.ActorFromBootstrap(bootstrap.Value!, identity!))
        : CommandResult<ActorContext>.Fail(bootstrap.ErrorCode ?? "AUTH_REQUIRED");
}

static IResult ToHttp<T>(CommandResult<T> result) =>
    result.Succeeded
        ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.ErrorCode });

static byte[] DecodeUploadBytes(RecordUploadBytesApiRequest request)
{
    if (!string.IsNullOrWhiteSpace(request.BytesBase64))
    {
        return Convert.FromBase64String(request.BytesBase64);
    }

    return Encoding.UTF8.GetBytes(request.CsvText ?? string.Empty);
}

public sealed record IdempotentApiRequest(string IdempotencyKey);
public sealed record RecordUploadBytesApiRequest(string WriteCapabilityId, string? CsvText, string? BytesBase64, string IdempotencyKey);
public sealed record ReviseSetupPresetApiRequest(string Label, IReadOnlyList<ChecklistItemInput> Checklist, string IdempotencyKey);
public sealed record RevisePlanApiRequest(
    string SetupPresetRevisionId,
    string EntryZoneLow,
    string EntryZoneHigh,
    string InitialStop,
    string PlannedRiskUsdt,
    int Confidence,
    string? Thesis,
    int? ExpiryDurationSeconds,
    string IdempotencyKey);
public sealed record AbandonMeasurementApiRequest(string Reason, string IdempotencyKey);
public sealed record ConfirmBehavioralExperimentApiRequest(int ExpectedRevisionNo, string IdempotencyKey);
public sealed record CancelBehavioralExperimentApiRequest(int ExpectedRevisionNo, string Reason, string IdempotencyKey);
public sealed record ReviseEpisodeReviewApiRequest(
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

public partial class Program;
