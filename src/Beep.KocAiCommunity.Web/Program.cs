using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.Ui.Studio;
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

// The Web calls the API (never the DB) — in Production it just needs real authentication configured.
KocProductionPreflight.Validate(builder.Configuration, builder.Environment, checkDatabase: false, checkSeed: false);

builder.AddServiceDefaults();

// Where people sign in — the one thing the first run settles, read by both hosts.
builder.Services.AddKocSetup();
builder.Services.AddKocStudioUi();
var setup = new KocSetupStore(builder.Configuration);
var demoPersonas = setup.DemoPersonasEnabled;

builder.Services.AddKocCurrentUser();
builder.Services.AddKocAuthorization();
builder.Services.AddKocWebAuthentication(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();

// The corporate account arrives already verified — by IIS when hosted there, by Negotiate otherwise.
// Registered unconditionally so an install can recognise a KOC deployment on the first request rather
// than asking; where IIS handles it in-process, its own scheme answers first and this is unused.
builder.Services.AddAuthentication().AddNegotiate();

// Typed API client. The Web calls /api/v1 and never touches the database directly. The current user is
// per-circuit, never shared between visitors, unless this is a demo host with a persona switcher.
var apiBaseUrl = builder.Configuration["KocApi:BaseUrl"] ?? "http://localhost:5250";
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

// Before anything renders: the page shell reads the resolved language to set <html lang> and <dir>.
app.UseRequestLocalization();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Beep.KocAiCommunity.Ui.Community.UiCommunityAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Studio.UiStudioAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Admin.UiAdminAssembly).Assembly);

app.Run();
