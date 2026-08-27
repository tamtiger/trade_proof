var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/healthz"));
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    service = "TradeProof.Api",
    phase = "phase-0"
}));

app.Run();

public partial class Program;
