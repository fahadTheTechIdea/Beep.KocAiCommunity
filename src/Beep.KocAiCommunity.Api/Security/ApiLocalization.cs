using System.Globalization;
using Beep.KocAiCommunity.Contracts.Localization;
using Microsoft.AspNetCore.Localization;

namespace Beep.KocAiCommunity.Api.Security;

/// <summary>
/// What language a request wants its content in.
/// <para>
/// The API has no page of its own, so there is no cookie and no session to read — the caller says what
/// it wants with <c>Accept-Language</c>, which the Web sets from the language the reader chose. That
/// keeps one answer for the whole round trip: the chrome and the content that fills it agree.
/// </para>
/// <para>
/// As on the Web, only the language changes. Formatting stays pinned so a score serialised by the API
/// is the same score however it was asked for.
/// </para>
/// </summary>
public static class ApiLocalization
{
    public static IServiceCollection AddKocApiLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var formatting = new CultureInfo(KocLanguages.FormattingCulture);
            options.DefaultRequestCulture = new RequestCulture(formatting, new CultureInfo(KocLanguages.English));
            options.SupportedCultures = [formatting];
            options.SupportedUICultures = [.. KocLanguages.All.Select(l => new CultureInfo(l))];
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });

        return services;
    }

    /// <summary>The language this request asked for, normalised, defaulting to English.</summary>
    public static string RequestLanguage(this HttpContext? http) =>
        KocLanguages.Normalize(
            http?.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.TwoLetterISOLanguageName
            ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
