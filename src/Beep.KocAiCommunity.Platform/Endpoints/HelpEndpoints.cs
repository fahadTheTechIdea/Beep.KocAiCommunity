using Beep.KocAiCommunity.Platform.Security;
using Beep.KocAiCommunity.Application.Help;
using Beep.KocAiCommunity.Infrastructure.Help;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Help;

namespace Beep.KocAiCommunity.Platform.Endpoints;

/// <summary>In-app help: browse/search articles and read one by slug.</summary>
public static class HelpEndpoints
{
    public static RouteGroupBuilder MapHelpEndpoints(this RouteGroupBuilder group)
    {
        // The Arabic articles are matched to the English ones by slug and swapped in. An article with
        // no translation keeps its English rather than disappearing — a help section that hid half of
        // itself would look broken rather than partly translated.
        group.MapGet("/help/articles", (HttpContext http, string? category, string? q, IHelpService help) =>
            Results.Ok(HelpDocument.Merge(help.List(category, q), http.RequestLanguage())
                .Select(a => new HelpArticleSummaryDto(a.Slug, a.Title, a.Category, a.Summary, a.Tags)).ToList()))
        .WithName("ListHelpArticles").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/help/categories", (IHelpService help) => Results.Ok(help.Categories))
            .WithName("ListHelpCategories").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/help/articles/{slug}", (HttpContext http, string slug, IHelpService help) =>
        {
            var a = help.Get(slug) is { } english
                ? HelpDocument.Merge([english], http.RequestLanguage()).FirstOrDefault()
                : null;
            return a is null
                ? Results.NotFound()
                : Results.Ok(new HelpArticleDto(a.Slug, a.Title, a.Category, a.Summary, a.BodyMarkdown, a.Tags));
        })
        .WithName("GetHelpArticle").RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
