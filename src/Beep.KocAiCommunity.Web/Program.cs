using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Platform;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Web.Components;
using Beep.KocAiCommunity.Web.Security;
using Beep.KocAiCommunity.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = KocHostEnvironment.Resolve(),
});

// The Web is the platform host now — it owns the database, so a misconfigured Production install
// (SQLite, seeding left on, or no real auth) has to be refused here rather than in a second process.
KocProductionPreflight.Validate(builder.Configuration, builder.Environment);

builder.AddServiceDefaults();

// The platform surface gets its own listener, bound to loopback, and is mapped only there. The public
// port serves pages and the leaderboard hub — /api/v1 does not exist on it, so there is nothing on the
// outside to reject or probe. Set Platform:InternalPort to 0 to skip the second listener entirely,
// which is what an in-process test host does.
var internalApiPort = builder.Configuration.GetValue("Platform:InternalPort", 5151);
if (internalApiPort > 0)
{
    // The public listener is ADDED TO, never replaced. The host reads ASPNETCORE_URLS under the key
    // "urls" (the prefix is stripped), so checking only the long name found nothing and this bound the
    // site to loopback alone — the website itself disappeared. Falling back to the dev address keeps a
    // public listener even when nothing is configured at all.
    var configured = builder.Configuration["urls"] ?? builder.Configuration["ASPNETCORE_URLS"];
    var publicUrls = string.IsNullOrWhiteSpace(configured) ? "http://localhost:5150" : configured;
    builder.WebHost.UseUrls($"{publicUrls};http://127.0.0.1:{internalApiPort}");
}

// Where people sign in — the one thing the first run settles, read by both hosts.
builder.Services.AddKocSetup();
var setup = new KocSetupStore(builder.Configuration);
var demoPersonas = setup.DemoPersonasEnabled;

// The platform surface: persistence, ML, endpoints, hub. AddKocCurrentUser/AddKocAuthorization come
// with it, so they are not repeated here. Authentication is the host's, not the surface's — the
// website signs people in with a cookie and must keep that as its default scheme.
builder.Services.AddKocPlatform(builder.Configuration, registerAuthentication: false);
builder.Services.AddKocPlatformRateLimiting();
builder.Services.AddKocOutboxDispatcher(builder.Configuration);

builder.Services.AddKocWebAuthentication(builder.Configuration);

// …and the platform's own access token beside it, because KOC Studio on the desktop and any other
// API caller authenticate with a bearer token, not a browser cookie.
builder.Services.AddKocPlatformBearer(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();

// The corporate account arrives already verified — by IIS when hosted there, by Negotiate otherwise.
// On by default so an install recognises a KOC deployment on the first request rather than asking;
// where IIS handles it in-process, its own scheme answers first and this is unused.
//
// Switchable because Negotiate is a *request* handler: the authentication middleware runs it on every
// request whatever the default scheme is, and it demands a server with IConnectionItemsFeature. Under
// a test server there is none, so every call died with a 500 before reaching its endpoint — which is
// exactly what happened when the platform surface moved into this host.
if (builder.Configuration.GetValue("Auth:EnableNegotiate", true))
{
    builder.Services.AddAuthentication().AddNegotiate();
}

// Typed API client. The platform surface is in this same process now, so the default points at this
// host — a loopback call, not a second website. KocApi:BaseUrl still overrides it for an install that
// genuinely does keep the surface somewhere else. The current user is per-circuit, never shared
// between visitors, unless this is a demo host with a persona switcher.
var apiBaseUrl = builder.Configuration["KocApi:BaseUrl"]
    ?? (internalApiPort > 0 ? $"http://127.0.0.1:{internalApiPort}" : "http://localhost:5150");
builder.Services.AddKocHttpClient(apiBaseUrl, perUserIdentity: !demoPersonas, forwardDevHeaders: demoPersonas);

// Every API call carries the signed-in user's platform token — whether they got it from a password here
// or from the corporate account IIS verified. One path, both deployments.
builder.Services.AddTransient<ApiTokenForwardingHandler>();
builder.Services.AddHttpClient<IKocApiClient, KocApiClient>()
    .AddHttpMessageHandler<ApiTokenForwardingHandler>();

// A bare client for the identity exchange: it runs before anyone is signed in, so it must not go
// through the handler that attaches a token.
builder.Services.AddHttpClient(KocExchangeClient.Name, client => client.BaseAddress = new Uri(apiBaseUrl));

// Blazor Web App with global Interactive Server render mode.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// English and Arabic. The cookie is the primary store because the pages that most need Arabic are open
// to people with no account; see KocLocalization for how the choice is resolved and remembered.
builder.Services.AddKocLocalization();

// Writes competition hero images into wwwroot so they are served as ordinary static files.
builder.Services.AddScoped<Beep.KocAiCommunity.Web.Services.HeroImageStorage>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serve runtime-written static files (e.g. uploaded competition hero images under wwwroot/uploads).
// MapStaticAssets only serves build-time assets, so this handles files added while the app runs.
app.UseStaticFiles();

// /api/v1 is for this process only. Since the surface merged in here and the desktop went
// database-direct, nothing off this machine calls it — so it is closed rather than left open for a
// caller that no longer exists. The leaderboard hub stays reachable; KOC Studio still uses it.
app.UseKocInternalApiOnly(app.Configuration.GetValue(InternalApiGuard.ConfigurationKey, true));

// Before anything renders: the page shell reads the resolved language to set <html lang> and <dir>.
app.UseRequestLocalization();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Inside KOC the visitor is already authenticated by IIS: turn that into our session, and take it as
// the answer to where people sign in. Must run before the guard, or a corporate deployment would be
// sent to a wizard it never needs.
app.UseKocEnvironmentSignIn();

// Until setup has been settled there is nothing meaningful to show — send visitors to the wizard.
app.UseKocSetupGuard();

app.MapStaticAssets();

// Sign-in form posts (they set the auth cookie, which a Blazor circuit cannot do) and the wizard's save.
app.MapKocAccountEndpoints();
app.MapKocSetupEndpoints();
app.MapKocCultureEndpoints();

// /health and /alive (Phase 01 acceptance gate). Entra auth arrives in Phase 02.
app.MapDefaultEndpoints();

// The platform surface — /api/v1 and the leaderboard hub — served from this host.
app.MapKocPlatform(internalApiPort > 0
    ? [$"127.0.0.1:{internalApiPort}", $"localhost:{internalApiPort}"]
    : null);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Beep.KocAiCommunity.Ui.Community.UiCommunityAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Admin.UiAdminAssembly).Assembly);

await app.Services.UseKocPlatformDatabaseAsync(app.Configuration);

// Reclaim pipeline scratch a previous run could not delete (a crash). Runs clean up promptly during
// execution; this is only the safety net.
Beep.KocAiCommunity.ML.Nodes.PipelineTemp.SweepOrphans(TimeSpan.FromHours(2));

app.Run();

/// <summary>Exposed so integration and end-to-end tests can boot this host with WebApplicationFactory.</summary>
public partial class Program;
