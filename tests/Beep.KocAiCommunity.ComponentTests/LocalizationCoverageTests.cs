using System.Reflection;
using Beep.KocAiCommunity.Ui.Shared.Branding;
using Beep.KocAiCommunity.Ui.Shared.Components;
using Beep.KocAiCommunity.Web.Components.Shared;
using Beep.KocAiCommunity.Web.Security;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// Holds the Arabic resource to the markup.
/// <para>
/// A missing translation is not an error at runtime — <c>IStringLocalizer</c> returns the English key,
/// which is exactly what makes a gradual rollout safe. It is also what makes an untranslated string
/// invisible: the page looks fine to whoever wrote it, and only an Arabic reader ever finds out. This
/// test is the difference between "bilingual" being a property of the build and being a claim.
/// </para>
/// </summary>
public class LocalizationCoverageTests
{
    /// <summary>Every <c>L["..."]</c> in the markup, including the two-line form the formatter produces.</summary>
    private static readonly Regex LocalizedString = new(@"L\[""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static readonly string Root =
        typeof(LocalizationCoverageTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RepositoryRoot").Value!;

    /// <summary>
    /// Where the interface lives. Admin.razor and WorkflowDesigner.razor are deliberately absent: their
    /// audience is a handful of people who work in English, and translating them was scoped out.
    /// </summary>
    private static readonly string[] ProjectsInScope =
    [
        "src/Beep.KocAiCommunity.Web",
        "src/Beep.KocAiCommunity.Ui.Shared",
        "src/Beep.KocAiCommunity.Ui.Studio",
        "src/Beep.KocAiCommunity.Ui.Community",

        // The desktop Studio shares the same resource, so its strings are held to the same standard.
        // Without it here, every string added to the desktop reads as an orphan.
        "src/Beep.KocAiCommunity.WinForms",
    ];

    private static readonly string[] OutOfScopeFiles = ["Admin.razor", "WorkflowDesigner.razor"];

    private static IEnumerable<string> MarkupFiles() =>
        ProjectsInScope
            .Select(p => Path.Combine(Root, p.Replace('/', Path.DirectorySeparatorChar)))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !OutOfScopeFiles.Contains(Path.GetFileName(f)));

    private static Dictionary<string, string> ArabicEntries()
    {
        var path = Path.Combine(Root, "src", "Beep.KocAiCommunity.Ui.Shared", "Localization", "Strings.ar.resx");
        File.Exists(path).Should().BeTrue("the Arabic resource is at {0}", path);

        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")?.Value ?? string.Empty);
    }

    /// <summary>
    /// Label sets declared in C# rather than markup. <c>@L[CompetitionDisplay.StatusLabel(x)]</c> passes
    /// a computed key, so the markup scan cannot see it — these classes declare what they can return so
    /// it stays covered. A label added to one of those switches but not to its Translatable array is
    /// exactly the silent gap this whole test exists to prevent.
    /// </summary>
    private static IEnumerable<(string File, string Key)> DeclaredLabels() =>
        new[]
        {
            (nameof(MlTaskLabels), MlTaskLabels.Translatable),
            (nameof(CompetitionDisplay), CompetitionDisplay.Translatable),
            (nameof(SignInPrompt), SignInPrompt.Translatable),

            // The masthead. Constants rather than literals, so the markup scan cannot see them, and an
            // untranslated one is the most visible gap on the site — it sits above every page.
            (nameof(KocBrand), KocBrand.Translatable),
        }
        .SelectMany(set => set.Item2.Select(key => (File: set.Item1 + ".cs", Key: key)));

    /// <summary>(file, key) for every localized string in the markup, plus the label sets declared in code.</summary>
    private static List<(string File, string Key)> UsedKeys() =>
        [.. MarkupFiles().SelectMany(file =>
            LocalizedString.Matches(File.ReadAllText(file))
                .Select(m => (File: Path.GetFileName(file), Key: m.Groups[1].Value.Replace("\\\"", "\"")))),
          .. DeclaredLabels()];

    [Fact]
    public void The_scan_finds_the_markup_it_is_supposed_to_guard()
    {
        // A coverage test that silently matches nothing passes forever and guards nothing. This is the
        // canary: if the repository moves or the regex stops matching, this fails first and says so.
        MarkupFiles().Should().NotBeEmpty("the scan must actually reach the .razor sources under {0}", Root);
        UsedKeys().Should().NotBeEmpty("at least one page is localized, so the pattern must match something");
    }

    [Fact]
    public void Every_localized_string_has_an_arabic_translation()
    {
        var arabic = ArabicEntries();

        var missing = UsedKeys()
            .Where(u => !arabic.ContainsKey(u.Key))
            .GroupBy(u => u.Key)
            .Select(g => $"  \"{g.Key}\"  — used in {string.Join(", ", g.Select(x => x.File).Distinct())}")
            .ToList();

        missing.Should().BeEmpty(
            "every string wrapped in L[\"...\"] needs an entry in Strings.ar.resx, or it silently renders "
            + "English to an Arabic reader. Missing:\n{0}", string.Join("\n", missing));
    }

    [Fact]
    public void No_arabic_entry_is_left_empty()
    {
        // An empty value is worse than a missing one: the localizer returns "" and the label vanishes
        // from the page entirely, rather than falling back to readable English.
        ArabicEntries().Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key)
            .Should().BeEmpty("an empty translation renders as nothing at all");
    }

    [Fact]
    public void The_arabic_resource_has_no_orphans()
    {
        // English text is the key, so editing a label in the markup orphans its translation. Nothing
        // breaks — the new English shows — which is precisely why it needs pointing out.
        var used = UsedKeys().Select(u => u.Key).ToHashSet(StringComparer.Ordinal);

        ArabicEntries().Keys.Where(k => !used.Contains(k))
            .Should().BeEmpty("these translations no longer match any string in the markup — the English "
                            + "text was probably edited, which changes the key");
    }

    [Fact]
    public void No_two_entries_differ_only_by_case()
    {
        // .resx resource names are case-insensitive. "revealed" and "Revealed" are one entry, and the
        // build resolves the clash by dropping one with a warning nobody reads — so one of the two
        // strings silently loses its translation. Caught once already; this stops it recurring.
        var collisions = ArabicEntries().Keys
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" / ", g))
            .ToList();

        collisions.Should().BeEmpty(
            "these keys differ only by case, so .resx treats them as one entry: {0}",
            string.Join(" | ", collisions));
    }

    [Fact]
    public void Nothing_in_scope_still_carries_the_old_per_page_language_toggle()
    {
        // Learn.razor had its own language switch before there was a global one. Two controls that both
        // claim to change the language, doing different things, is the state this replaced.
        foreach (var file in MarkupFiles())
        {
            File.ReadAllText(file).Should().NotContain("TrackLanguages.All",
                "{0} should use the app-bar switcher, not a toggle of its own", Path.GetFileName(file));
        }
    }
}
