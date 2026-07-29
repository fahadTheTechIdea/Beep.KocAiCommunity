using System.Globalization;
using System.Reflection;
using Beep.KocAiCommunity.Domain.Learning;

namespace Beep.KocAiCommunity.Infrastructure.Learning;

/// <summary>
/// One authored track, read from a markdown document rather than a C# string literal.
/// <para>
/// Track content is prose — a dozen tracks of it. Kept in source as escaped strings it is unreadable in
/// review and unwritable by anyone who isn't a C# developer, which is the wrong constraint for material
/// the training team owns. As markdown it reads as what it is, diffs sensibly, and stays a single source
/// of truth: the documents are embedded in the assembly, so the seeder needs no files on disk.
/// </para>
/// </summary>
public sealed record TrackDocument(
    string Title,
    string Summary,
    TrackLevel Level,
    int Order,
    IReadOnlyList<(string Title, string? Content)> Lessons)
{
    /// <summary>
    /// The language the document is written in. Declared with a <c>language:</c> header; English when
    /// absent, because every track that existed before translation was English.
    /// </summary>
    public string Language { get; init; } = TrackLanguages.English;

    /// <summary>
    /// Ties a document to its translations. Taken from the file name — <c>07-anomaly-detection.md</c> and
    /// <c>07-anomaly-detection.ar.md</c> both key on <c>07-anomaly-detection</c> — so a translator pairs a
    /// document by naming it, with no identifier to copy across and get wrong.
    /// </summary>
    public string ContentKey { get; init; } = string.Empty;

    private const string ResourcePrefix = "Beep.KocAiCommunity.Infrastructure.Learning.Content.";

    /// <summary>
    /// The headings that start a lesson — everything under one, up to the next, is its body. A translator
    /// writes the heading in the language they are writing in; requiring the English word inside an
    /// Arabic document would make the file unreadable to the person maintaining it. Only these prefixes
    /// start a lesson, so an ordinary <c>##</c> section inside a lesson body stays part of that body.
    /// </summary>
    private static readonly string[] LessonHeadings = ["## Lesson", "## الدرس"];

    /// <summary>Every track document embedded in this assembly, in the order the tracks should appear.</summary>
    public static IReadOnlyList<TrackDocument> All() =>
        [.. typeof(TrackDocument).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Select(Load)
            .OfType<TrackDocument>()
            .OrderBy(doc => doc.Order)
            .ThenBy(doc => doc.Language, StringComparer.Ordinal)];

    private static TrackDocument? Load(string resourceName)
    {
        using var stream = typeof(TrackDocument).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        var document = Parse(reader.ReadToEnd());
        return document is null ? null : document with { ContentKey = ContentKeyFrom(resourceName) };
    }

    /// <summary>
    /// The content key a resource name implies. Embedded resource names replace directory separators with
    /// dots, so <c>…Content.07-anomaly-detection.ar.md</c> arrives flat: strip the prefix, the
    /// <c>.md</c>, and a trailing language suffix, and what's left names the material.
    /// </summary>
    private static string ContentKeyFrom(string resourceName)
    {
        var name = resourceName[ResourcePrefix.Length..^".md".Length];

        foreach (var language in TrackLanguages.All)
        {
            var suffix = "." + language;
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }

        return name;
    }

    /// <summary>
    /// Splits a document into its header and lessons. The header is a short key/value block; each
    /// <c>## Lesson N — Title</c> heading starts a lesson whose body runs to the next heading.
    /// </summary>
    public static TrackDocument? Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        string? title = null, summary = null;
        var level = TrackLevel.Beginner;
        var order = 0;
        var language = TrackLanguages.English;

        var lessons = new List<(string Title, string? Content)>();
        string? lessonTitle = null;
        var body = new List<string>();

        void FlushLesson()
        {
            if (lessonTitle is not null)
            {
                lessons.Add((lessonTitle, string.Join('\n', body).Trim()));
            }

            body.Clear();
        }

        foreach (var line in lines)
        {
            if (LessonHeadings.FirstOrDefault(h => line.StartsWith(h, StringComparison.Ordinal)) is { } marker)
            {
                FlushLesson();

                // "## Lesson 3 — Reading the metrics" → "Reading the metrics". Em dash or hyphen.
                var heading = line[marker.Length..].Trim();
                var separator = heading.IndexOfAny(['—', '-', ':']);
                lessonTitle = (separator >= 0 ? heading[(separator + 1)..] : heading).Trim();
                continue;
            }

            if (lessonTitle is not null)
            {
                body.Add(line);
                continue;
            }

            // Still in the header block.
            if (TryHeader(line, "title:", out var headerValue)) { title = headerValue; }
            else if (TryHeader(line, "summary:", out headerValue)) { summary = headerValue; }
            else if (TryHeader(line, "order:", out headerValue)) { order = int.TryParse(headerValue, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0; }
            else if (TryHeader(line, "language:", out headerValue)) { language = TrackLanguages.Normalize(headerValue); }
            else if (TryHeader(line, "level:", out headerValue) && Enum.TryParse<TrackLevel>(headerValue, ignoreCase: true, out var parsedLevel)) { level = parsedLevel; }
        }

        FlushLesson();

        return title is null || summary is null || lessons.Count == 0
            ? null
            : new TrackDocument(title, summary, level, order, lessons) { Language = language };
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
