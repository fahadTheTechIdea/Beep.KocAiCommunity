using Beep.KocAiCommunity.Ui.Shared.Branding;
using MudBlazor;

namespace Beep.KocAiCommunity.Ui.Shared.Components;

/// <summary>
/// The worked example in the home-page hero: one prediction, start to finish, on something the reader
/// recognises.
/// </summary>
/// <param name="Subject">What is being looked at — a pump, a well, a payroll run.</param>
/// <param name="Caption">What the readings above the chart are.</param>
/// <param name="Readings">Three column names, the model's inputs. Exactly three; more crowds the card.</param>
/// <param name="Verdict">What the model concluded.</param>
/// <param name="Advice">What somebody does about it — the reason the prediction was worth making.</param>
/// <param name="Points">The trace, as SVG polyline points in a 320×74 box.</param>
/// <param name="Image">A KOC domain icon, where one fits the subject.</param>
/// <param name="Glyph">A Material icon, for the subjects the oilfield icon set has nothing for.</param>
public sealed record HeroShot(
    string Subject,
    string Caption,
    string[] Readings,
    string Verdict,
    string Advice,
    string Points,
    string? Image = null,
    string Glyph = "");

/// <summary>
/// One hero example per area of KOC.
/// <para>
/// A production engineer who filters the page down to their own discipline should not still be looking
/// at somebody else's pump, and a colleague from HR has no reason to recognise a vibration trace at all.
/// The example is the single thing on the page that explains what machine learning is to a reader who
/// has never met it, so it has to be drawn from a world they already know.
/// </para>
/// <para>
/// Text here is passed to the localizer as a computed key, which the markup scan cannot see — hence
/// <see cref="Translatable"/>, which holds every string in this file to the same Arabic coverage as a
/// literal in a .razor file.
/// </para>
/// </summary>
public static class HeroShots
{
    // Traces, all in the same 320×74 box. Their shape is part of the story: a fault builds, a well
    // declines, a log wanders, a payroll run sits flat until one month does not.
    private const string Rising = "0,54 26,50 52,56 78,46 104,52 130,44 156,48 182,38 208,42 234,30 260,26 286,16 312,8 320,6";
    private const string Falling = "0,10 26,14 52,12 78,20 104,18 130,26 156,30 182,28 208,38 234,42 260,48 286,54 312,60 320,62";
    private const string Wandering = "0,40 26,22 52,52 78,30 104,58 130,26 156,44 182,18 208,50 234,34 260,60 286,28 312,46 320,38";
    private const string Dipping = "0,22 26,20 52,26 78,24 104,34 130,46 156,52 182,48 208,40 234,34 260,30 286,28 312,26 320,25";
    private const string Stepping = "0,20 26,22 52,20 78,24 104,22 130,26 156,44 182,46 208,48 234,47 260,50 286,52 312,54 320,55";
    private const string Spiking = "0,42 26,40 52,44 78,41 104,43 130,40 156,8 182,42 208,44 234,41 260,43 286,42 312,40 320,41";

    /// <summary>The one shown when no area is chosen: equipment everybody on site knows.</summary>
    public static readonly HeroShot Default = new(
        "Pump P-114", "Live sensor readings",
        ["vibration", "motor temp", "current"],
        "Likely to fail within 12 days", "Book the service now, not at 3am",
        Rising, Image: "034-pump-1.png");

    private static readonly Dictionary<string, HeroShot> ByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["maintenance"] = Default,

        ["subsurface"] = new(
            "Well A-27, at 2,340 m", "One interval of the wireline log",
            ["gamma ray", "density", "neutron"],
            "This interval is sandstone", "Pick the pay zone without waiting on core",
            Wandering, Image: "039-oil-well.png"),

        ["drilling-wells"] = new(
            "Well B-9, current section", "Readings from the last stand drilled",
            ["weight on bit", "rotary speed", "mud flow"],
            "The bit is about to slow down", "Change it on this trip, not the next one",
            Falling, Image: "063-oil-rig.png"),

        ["production"] = new(
            "Well C-41, last 90 days", "Daily production history",
            ["oil rate", "water cut", "wellhead pressure"],
            "Below target rate within six weeks", "Plan the intervention before it gets there",
            Falling, Image: "220-pump-jack.png"),

        ["facilities"] = new(
            "Gas train 2", "Plant readings across this shift",
            ["inlet pressure", "compressor duty", "ambient temp"],
            "Throughput is 6% under target", "And the cause is upstream, not the compressor",
            Dipping, Image: "025-compressor.png"),

        ["hse"] = new(
            "A near-miss report", "What the reporter wrote down",
            ["task type", "location", "hours into shift"],
            "This one could have hurt somebody", "Escalate it before the pattern repeats",
            Stepping, Glyph: Icons.Material.Filled.HealthAndSafety),

        ["medical"] = new(
            "A routine lab panel", "Results from one screening visit",
            ["fasting glucose", "HbA1c", "body mass index"],
            "Worth screening for diabetes", "Ask them back before they feel unwell",
            Rising, Glyph: Icons.Material.Filled.MedicalServices),

        ["training"] = new(
            "A course with 24 seats booked", "What is known on enrolment day",
            ["booked ahead", "courses finished", "workload"],
            "Six of them will not finish", "A nudge in week one beats a wasted seat in week six",
            Stepping, Glyph: Icons.Material.Filled.School),

        ["people"] = new(
            "One month's payroll run", "Every component of the run",
            ["hours", "base pay", "overtime"],
            "This run breaks the pattern", "Check it before it is paid, not after",
            Spiking, Glyph: Icons.Material.Filled.Payments),
    };

    /// <summary>The example for the chosen area, or the pump when nothing is chosen.</summary>
    public static HeroShot For(string? categoryCode) =>
        categoryCode is { Length: > 0 } code && ByCategory.TryGetValue(code, out var shot) ? shot : Default;

    /// <summary>The KOC icon for a shot, or null when it uses a Material glyph instead.</summary>
    public static string? ImagePath(HeroShot shot) => shot.Image is { Length: > 0 } file ? KocBrand.Icon(file) : null;

    /// <summary>The trace closed down to the baseline, so the area under it can be filled.</summary>
    public static string AreaPath(HeroShot shot) => $"M{shot.Points.Replace(" ", " L")} L320,74 L0,74 Z";

    /// <summary>Where to put the marker: the last point of the trace, which is the reading being judged.</summary>
    public static (string X, string Y) Endpoint(HeroShot shot)
    {
        var last = shot.Points.Split(' ')[^1].Split(',');
        return (last[0], last[1]);
    }

    /// <summary>Every string in this file that reaches a reader, for the Arabic coverage test.</summary>
    public static readonly string[] Translatable =
        [.. ByCategory.Values.Append(Default).Distinct()
            .SelectMany(s => new[] { s.Subject, s.Caption, s.Verdict, s.Advice }.Concat(s.Readings))
            .Distinct()];
}
