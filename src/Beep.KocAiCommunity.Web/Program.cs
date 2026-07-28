using Beep.KocAiCommunity.Client;
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

// The Web calls the API (never the DB) — in Production it just needs real authentication configured.
KocProductionPreflight.Validate(builder.Configuration, builder.Environment, checkDatabase: false, checkSeed: false);

builder.AddServiceDefaults();

// How this installation signs people in — chosen in the first-run wizard, read by both hosts.
builder.Services.AddKocSetup();
var setup = new KocSetupStore(builder.Configuration);
var authMode = setup.Mode;

// KOC security: current-user accessor + authorization policies always; the sign-in scheme follows the mode.
builder.Services.AddKocCurrentUser();
builder.Services.AddKocAuthorization();
builder.Services.AddKocWebAuthentication(builder.Configuration);
builder.Services.AddCascadingAuthenticationState();

// Typed API client. The Web calls /api/v1 and never touches the database directly. Once a real sign-in
// mode is configured the "current user" is per-circuit, never shared between visitors.
var apiBaseUrl = builder.Configuration["KocApi:BaseUrl"] ?? "http://localhost:5250";
var realSignIn = authMode is not (KocAuthMode.DemoPersonas or KocAuthMode.Unconfigured);
builder.Services.AddKocHttpClient(apiBaseUrl, perUserIdentity: realSignIn, forwardDevHeaders: !realSignIn);

if (authMode == KocAuthMode.LocalAccounts)
{
    // Present the signed-in user's API access token instead of asserting an identity in a header.
    builder.Services.AddTransient<ApiTokenForwardingHandler>();
    builder.Services.AddHttpClient<IKocApiClient, KocApiClient>()
        .AddHttpMessageHandler<ApiTokenForwardingHandler>();
}
else if (authMode == KocAuthMode.WindowsIntranet)
{
    // Intranet SSO: forward the real signed-in Windows account to the API.
    builder.Services.AddTransient<WindowsIdentityForwardingHandler>();
    builder.Services.AddHttpClient<IKocApiClient, KocApiClient>()
        .AddHttpMessageHandler<WindowsIdentityForwardingHandler>();
}

// Blazor Web App with global Interactive Server render mode.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Until setup has been completed there is nothing meaningful to show — send every visitor to the wizard.
app.UseKocSetupGuard();

app.MapStaticAssets();

// Sign-in form posts (they set the auth cookie, which a Blazor circuit cannot do) and the wizard's save.
if (authMode == KocAuthMode.LocalAccounts)
{
    app.MapKocAccountEndpoints();
}

app.MapKocSetupEndpoints();

// /health and /alive (Phase 01 acceptance gate). Entra auth arrives in Phase 02.
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Beep.KocAiCommunity.Ui.Community.UiCommunityAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Studio.UiStudioAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Admin.UiAdminAssembly).Assembly);

app.Run();
