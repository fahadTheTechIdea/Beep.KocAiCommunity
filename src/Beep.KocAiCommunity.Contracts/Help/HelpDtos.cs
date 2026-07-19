namespace Beep.KocAiCommunity.Contracts.Help;

/// <summary>A help article in a list — no body.</summary>
public sealed record HelpArticleSummaryDto(string Slug, string Title, string Category, string Summary, IReadOnlyList<string> Tags);

/// <summary>A full help article including its Markdown body.</summary>
public sealed record HelpArticleDto(string Slug, string Title, string Category, string Summary, string BodyMarkdown, IReadOnlyList<string> Tags);
