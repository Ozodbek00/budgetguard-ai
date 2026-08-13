using BudgetGuard.Api.Endpoints;
using BudgetGuard.Api.Infrastructure;
using BudgetGuard.Application;
using BudgetGuard.Application.Datasets;
using BudgetGuard.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Behind a TLS-terminating reverse proxy the app sees plain HTTP. Swagger UI
// builds its "try it out" request URLs from the request it was served on, so
// without this every call it generates points at http:// and is blocked as
// mixed content on an https:// page.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BudgetGuard AI",
        Version = "v1",
        Description =
            "Explainable statistical forensics for government procurement and budget spending. " +
            "Every flag carries a plain-language reason an auditor can verify by hand. " +
            "Demo datasets are synthetic and are never real government data."
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, "BudgetGuard.Api.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

// The maximum upload this instance will accept. Kept modest because the whole
// dataset is held in memory during analysis by design (see DatasetRepository).
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 32 * 1024 * 1024;
});

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();
await app.Services.SeedDemoDataIfEmptyAsync(app.Configuration);

// Must run before anything that reads the scheme or the client address.
app.UseForwardedHeaders();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "BudgetGuard AI v1");
    options.DocumentTitle = "BudgetGuard AI — API";
});

app.MapDatasetEndpoints();
app.MapAnalysisEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck")
    .WithTags("Diagnostics");

app.Run();

/// <summary>Exposed so the API can be driven from integration tests.</summary>
public partial class Program;
