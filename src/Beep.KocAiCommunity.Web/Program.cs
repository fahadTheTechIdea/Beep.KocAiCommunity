using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.Web.Components;
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

// KOC security: current-user accessor + authorization policies always; Entra sign-in only when configured.
builder.Services.AddKocCurrentUser();
builder.Services.AddKocAuthorization();
builder.Services.AddKocWebAuthentication(builder.Configuration);

// Typed API client. The Web calls /api/v1 and never touches the database directly.
var apiBaseUrl = builder.Configuration["KocApi:BaseUrl"] ?? "http://localhost:5250";
builder.Services.AddKocHttpClient(apiBaseUrl);

// Intranet Windows auth (opt-in, IIS Windows Authentication): forward the real signed-in user to the
// API instead of the dev persona. Off by default so dev/tests use the persona switcher unchanged.
if (SecurityExtensions.IsWindowsAuthEnabled(builder.Configuration))
{
    builder.Services.AddTransient<WindowsIdentityForwardingHandler>();
    builder.Services.AddHttpClient<IKocApiClient, KocApiClient>()
        .AddHttpMessageHandler<WindowsIdentityForwardingHandler>();
}

// Blazor Web App with global Interactive Server render mode.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// /health and /alive (Phase 01 acceptance gate). Entra auth arrives in Phase 02.
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Beep.KocAiCommunity.Ui.Community.UiCommunityAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Studio.UiStudioAssembly).Assembly,
        typeof(Beep.KocAiCommunity.Ui.Admin.UiAdminAssembly).Assembly);

app.Run();
