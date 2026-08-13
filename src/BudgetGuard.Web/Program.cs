using BudgetGuard.Application;
using BudgetGuard.Application.Datasets;
using BudgetGuard.Infrastructure;
using BudgetGuard.Web.Components;
using Microsoft.AspNetCore.HttpOverrides;

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

// Behind a TLS-terminating reverse proxy the app sees plain HTTP, so without
// this it believes it is running on http:// and generates absolute URLs and
// redirects on the wrong scheme.
//
// KnownNetworks and KnownProxies are cleared because a Docker network assigns
// the proxy an arbitrary private address that the default allow-list rejects.
// That is only safe because the container publishes no host ports and is
// reachable solely through that proxy — see deploy/docker-compose.shared-proxy.yml.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();
await app.Services.SeedDemoDataIfEmptyAsync(app.Configuration);

// Must run before anything that reads the scheme or the client address.
app.UseForwardedHeaders();

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
