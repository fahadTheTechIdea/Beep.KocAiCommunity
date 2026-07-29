using Beep.KocAiCommunity.Application.Localization;
using Microsoft.Extensions.Localization;

namespace Beep.KocAiCommunity.Api.Security;

/// <summary>
/// Turns a service's refusal into a sentence the caller can read, in the language they asked for.
/// </summary>
public static class ServiceMessageLocalization
{
    /// <summary>
    /// The localized message for an exception a member is meant to read.
    /// <para>
    /// The template is the resource key and the values are applied after translation, so
    /// "Daily submission quota ({0}) reached." works in either language while the number stays the
    /// number. An untranslated template falls back to its English, exactly as the interface does.
    /// </para>
    /// </summary>
    public static string For(this IStringLocalizer<ServiceMessages> messages, IUserFacingMessage ex) =>
        messages[ex.Template, ex.TemplateArgs];

    /// <summary>
    /// The one localizer the endpoint groups close over.
    /// <para>
    /// Captured when routes are mapped rather than injected per request, which is safe precisely
    /// because <see cref="IStringLocalizer"/> reads <c>CultureInfo.CurrentUICulture</c> on every call
    /// — the singleton answers each request in that request's language. The alternative was adding a
    /// parameter to twenty-two lambdas for no behavioural gain.
    /// </para>
    /// </summary>
    public static IStringLocalizer<ServiceMessages> ServiceMessages(this IEndpointRouteBuilder endpoints) =>
        endpoints.ServiceProvider.GetRequiredService<IStringLocalizer<ServiceMessages>>();
}
