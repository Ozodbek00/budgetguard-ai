using BudgetGuard.Application;
using BudgetGuard.Application.Datasets;
using BudgetGuard.Infrastructure;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using BudgetGuard.Domain.Detection.Explanations;
using BudgetGuard.Web.Components;
using BudgetGuard.Web.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;

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
// English, Uzbek and Russian. The detection explanations are translated too,
// not just the UI chrome — see BudgetGuard.Domain/Detection/Explanations.
var supportedCultures = ExplanationWriters.SupportedLanguages
    .Select(tag => new CultureInfo(tag))
    .ToArray();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Cookie first, then Accept-Language. An explicit choice must outrank the
    // browser's preference, or a reviewer who picks Uzbek on a machine set to
    // Russian would be switched back on the next page.
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddScoped<IUiText>(_ => UiTexts.For(CultureInfo.CurrentUICulture.Name));

// Let non-ASCII characters through as themselves. The default HtmlEncoder
// escapes everything outside Basic Latin into numeric entities, which is safe
// but turns every Cyrillic character into a seven-byte "&#1054;" — the Russian
// report page measured 116 KB instead of 45 KB, and its HTML source became
// unreadable. UTF-8 is declared on the page, so the escaping buys nothing here.
// Registered directly rather than through Configure<WebEncoderOptions>, which
// Blazor's renderer does not pick up — it resolves HtmlEncoder from DI.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

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
app.UseRequestLocalization();

app.UseStaticFiles();
app.UseAntiforgery();

// Switching language sets a cookie and reloads. It cannot be done inside the
// Blazor circuit: the culture is resolved once when the request that opened the
// circuit was handled, so changing it in-place would leave the server rendering
// in the old language until the next full load.
app.MapGet("/set-language", (string culture, string redirectUri, HttpContext http) =>
{
    if (!ExplanationWriters.IsSupported(culture))
    {
        return Results.BadRequest("Unsupported language.");
    }

    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

    // Only ever redirect within this site: an attacker-supplied absolute URL
    // here would turn the language switch into an open redirect.
    var target = Uri.IsWellFormedUriString(redirectUri, UriKind.Relative) ? redirectUri : "/";

    return Results.LocalRedirect(target);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
