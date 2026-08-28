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
    phase = "phase-2"
}));

app.MapGet("/openapi.json", () => Results.Ok(new
{
    openapi = "3.1.0",
    info = new { title = "TradeProof API", version = "phase-2" },
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
        "/api/imports/confirm",
        "/api/imports/{importBatchId}/progress"
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
            Encoding.UTF8.GetBytes(request.CsvText),
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

public sealed record IdempotentApiRequest(string IdempotencyKey);
public sealed record RecordUploadBytesApiRequest(string WriteCapabilityId, string CsvText, string IdempotencyKey);
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

public partial class Program;
