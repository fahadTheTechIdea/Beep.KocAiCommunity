namespace Beep.KocAiCommunity.Application.Help;

/// <summary>Serves the code-first help catalog: browse by category, read by slug, and full-text search.</summary>
public interface IHelpService
{
    IReadOnlyList<string> Categories { get; }

    /// <summary>Articles, optionally filtered by category and/or a free-text query (title/summary/body/tags).</summary>
    IReadOnlyList<HelpArticle> List(string? category, string? query);

    HelpArticle? Get(string slug);
}

/// <summary>Reads the static <see cref="HelpCatalog"/>. No storage — content lives in source.</summary>
public sealed class HelpService : IHelpService
{
    public IReadOnlyList<string> Categories => HelpCatalog.Categories;

    public IReadOnlyList<HelpArticle> List(string? category, string? query)
    {
        IEnumerable<HelpArticle> articles = HelpCatalog.All;

        if (!string.IsNullOrWhiteSpace(category))
        {
            articles = articles.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            articles = articles.Where(a => Matches(a, q));
        }

        return articles.ToList();
    }

    public HelpArticle? Get(string slug) => HelpCatalog.Find(slug);

    private static bool Matches(HelpArticle a, string q) =>
        a.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
        || a.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)
        || a.BodyMarkdown.Contains(q, StringComparison.OrdinalIgnoreCase)
        || a.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase));
}
