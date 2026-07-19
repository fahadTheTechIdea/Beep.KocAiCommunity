using Beep.KocAiCommunity.Application.Help;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Help;

namespace Beep.KocAiCommunity.Api.Endpoints;

/// <summary>In-app help: browse/search articles and read one by slug.</summary>
public static class HelpEndpoints
{
    public static RouteGroupBuilder MapHelpEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/help/articles", (string? category, string? q, IHelpService help) =>
            Results.Ok(help.List(category, q)
                .Select(a => new HelpArticleSummaryDto(a.Slug, a.Title, a.Category, a.Summary, a.Tags)).ToList()))
        .WithName("ListHelpArticles").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/help/categories", (IHelpService help) => Results.Ok(help.Categories))
            .WithName("ListHelpCategories").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/help/articles/{slug}", (string slug, IHelpService help) =>
        {
            var a = help.Get(slug);
            return a is null
                ? Results.NotFound()
                : Results.Ok(new HelpArticleDto(a.Slug, a.Title, a.Category, a.Summary, a.BodyMarkdown, a.Tags));
        })
        .WithName("GetHelpArticle").RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
