using System.Reflection;
using Beep.KocAiCommunity.Application.Help;
using Beep.KocAiCommunity.Contracts.Localization;

namespace Beep.KocAiCommunity.Infrastructure.Help;

/// <summary>
/// A translated help article, authored as markdown and embedded in this assembly.
/// <para>
/// The English articles live in <see cref="HelpCatalog"/> as C# strings. Prose belongs in markdown and
/// that catalogue should follow one day, but moving 469 working lines is a separate change with its own
/// risk — and doing it as part of a translation is how transcription errors get in. So the Arabic is
/// added alongside instead: same slugs, matched by <see cref="HelpArticle.Slug"/>, merged at read time.
/// </para>
/// <para>
/// The trap this shares with the learning tracks: MSBuild reads <c>getting-started.ar.md</c> as a
/// localized resource and moves it into an <c>ar/</c> satellite assembly where nothing finds it. The
/// csproj sets <c>WithCulture="false"</c> for exactly that reason.
/// </para>
/// </summary>
public static class HelpDocument
{
    private const string ResourcePrefix = "Beep.KocAiCommunity.Infrastructure.Help.Content.";

    private static readonly Lazy<IReadOnlyList<HelpArticle>> Translated = new(Load);

    /// <summary>Every translated article, in whatever languages have been authored.</summary>
    public static IReadOnlyList<HelpArticle> All(string language)
    {
        var wanted = KocLanguages.Normalize(language);
        return [.. Translated.Value.Where(a => LanguageOf(a) == wanted)];
    }

    /// <summary>
    /// The catalogue as one language's reader should see it: a translated article where one exists,
    /// the English otherwise. Never hides an article for lacking a translation — a partly translated
    /// help section that dropped its untranslated half would look broken rather than incomplete.
    /// </summary>
    public static IReadOnlyList<HelpArticle> Merge(IReadOnlyList<HelpArticle> english, string language)
    {
        var translated = All(language).ToDictionary(a => a.Slug, StringComparer.OrdinalIgnoreCase);
        return [.. english.Select(a => translated.GetValueOrDefault(a.Slug, a))];
    }

    /// <summary>The language an article is written in — the tag the parser stored.</summary>
    public static string LanguageOf(HelpArticle article) =>
        article.Tags.FirstOrDefault(t => t.StartsWith("lang:", StringComparison.Ordinal))?["lang:".Length..]
        ?? KocLanguages.English;

    private static IReadOnlyList<HelpArticle> Load() =>
        [.. typeof(HelpDocument).Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(Read)
            .OfType<HelpArticle>()];

    private static HelpArticle? Read(string resourceName)
    {
        using var stream = typeof(HelpDocument).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// Splits a document into its header and body. The header is the same shape the learning tracks
    /// use — <c>key: value</c> lines — and everything after the first blank line is the article.
    /// </summary>
    public static HelpArticle? Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        string? slug = null, title = null, category = null, summary = null;
        var language = KocLanguages.English;
        var tags = new List<string>();
        var body = new List<string>();
        var inBody = false;

        foreach (var line in lines)
        {
            if (inBody)
            {
                body.Add(line);
                continue;
            }

            if (line.Trim().Length == 0 && slug is not null)
            {
                inBody = true;
                continue;
            }

            if (TryHeader(line, "slug:", out var value)) { slug = value; }
            else if (TryHeader(line, "title:", out value)) { title = value; }
            else if (TryHeader(line, "category:", out value)) { category = value; }
            else if (TryHeader(line, "summary:", out value)) { summary = value; }
            else if (TryHeader(line, "language:", out value)) { language = KocLanguages.Normalize(value); }
            else if (TryHeader(line, "tags:", out value))
            {
                tags.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (slug is null || title is null || category is null || summary is null)
        {
            return null;
        }

        // The language rides along as a tag so HelpArticle needs no new field — search already looks at
        // tags, and "lang:ar" is not a word anybody searches for.
        tags.Add($"lang:{language}");

        return new HelpArticle(slug, title, category, summary, string.Join('\n', body).Trim(), tags);
    }

    private static bool TryHeader(string line, string key, out string value)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            value = trimmed[key.Length..].Trim();
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }
}
