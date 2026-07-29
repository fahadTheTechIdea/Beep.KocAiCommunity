using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Localization;

/// <summary>
/// One translated field of one piece of platform content — a competition category's name, a badge's
/// description — keyed by the row's stable code rather than by its English text.
/// <para>
/// Interface strings live in a .resx keyed on their English, which works because the English is in the
/// source and changing it is a code change. This content is different: it is rows in the database that
/// an administrator may rename. Keying the Arabic on the English would silently orphan it the moment
/// somebody edited a category name in the console. Keying on the <see cref="EntityKey"/> — the code,
/// which does not change — means a rename leaves the Arabic attached, merely out of date, which is a
/// problem an administrator can see and fix rather than one that vanishes.
/// </para>
/// <para>
/// Only content KOC ships is translated this way. What a colleague writes — a competition they host, a
/// discussion they post — stays in the language they wrote it in; nobody can be asked for a bilingual
/// post, and pretending otherwise would put empty Arabic in front of readers.
/// </para>
/// </summary>
public class ContentTranslation : AuditableEntity
{
    /// <summary>What kind of thing this translates, e.g. <c>competition-category</c>.</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>The row's stable identifier — its code, not its name.</summary>
    public string EntityKey { get; set; } = default!;

    /// <summary>Which field, e.g. <c>name</c> or <c>description</c>.</summary>
    public string Field { get; set; } = default!;

    /// <summary>ISO 639-1 language code.</summary>
    public string Language { get; set; } = default!;

    public string Text { get; set; } = default!;
}

/// <summary>The entity types carried in <see cref="ContentTranslation.EntityType"/>.</summary>
public static class TranslatedContent
{
    public const string CompetitionCategory = "competition-category";
    public const string Badge = "badge";

    public const string Name = "name";
    public const string Description = "description";
}
