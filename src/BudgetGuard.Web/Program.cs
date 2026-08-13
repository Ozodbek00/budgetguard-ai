using BudgetGuard.Application;
using BudgetGuard.Application.Datasets;
using BudgetGuard.Infrastructure;
using BudgetGuard.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The Blazor host composes the same application and infrastructure services the
// API does, and dispatches the same MediatR commands in-process. There is no
// HTTP hop between the UI and the analysis engine, and therefore no second
// implementation of anything that could drift from the API's behaviour.
// See docs/adr/0003-blazor-server-over-spa.md.
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();
await app.Services.SeedDemoDataIfEmptyAsync(app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Deliberately no UseHttpsRedirection: the container terminates plain HTTP and
// the hosting platform's proxy handles TLS. Redirecting here would send clients
// to a port the container does not listen on.
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
