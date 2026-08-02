using Beep.KocAiCommunity.Desktop.Local;
using Microsoft.Extensions.Logging;

namespace Beep.KocAiCommunity.WinForms.Diagnostics;

/// <summary>
/// Seeds a sample dataset on the very first launch.
/// <para>
/// A brand-new workspace is empty, and an empty designer is indistinguishable from a broken one. Landing
/// with something to run on turns the first five minutes from "why is there nothing here" into "what
/// does this do".
/// </para>
/// <para>
/// The file is named <c>sample-</c> deliberately. It is synthetic ESP readings shaped like the seeded
/// demo competition's data, so the desktop and the platform teach the same example — but nobody should
/// be able to mistake it for KOC data.
/// </para>
/// </summary>
public static class FirstRun
{
    private const string SampleFileName = "sample-esp-readings.csv";

    /// <summary>True when this launch is the first — the welcome page reads it.</summary>
    public static bool IsFirstLaunch { get; private set; }

    public static void EnsureSeeded(LocalWorkspace workspace, ILogger log)
    {
        if (File.Exists(workspace.FirstRunMarkerPath))
        {
            return;
        }

        IsFirstLaunch = true;

        try
        {
            var destination = Path.Combine(workspace.DatasetsPath, SampleFileName);
            if (!File.Exists(destination))
            {
                File.WriteAllText(destination, SampleCsv());
                log.LogInformation("Seeded the sample dataset for a first launch.");
            }

            // Written last: if seeding failed, the next launch should try again rather than leaving
            // the user with an empty workspace and no explanation.
            File.WriteAllText(workspace.FirstRunMarkerPath, DateTime.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            // An empty workspace is a poor first impression, not a reason to refuse to start.
            log.LogWarning(ex, "Could not seed the first-run sample.");
        }
    }

    /// <summary>
    /// Synthetic ESP readings: five correlated sensors and a rare fault, the same shape as the seeded
    /// anomaly competition. Generated rather than shipped as a file so it stays small and obviously fake.
    /// </summary>
    private static string SampleCsv()
    {
        // Fixed seed: everyone's first dataset is the same one, so a colleague's screenshot matches.
        var random = new Random(20260802);
        var rows = new System.Text.StringBuilder("id,intake_pressure,motor_temp,vibration,flow_rate,motor_current,failed\n");

        for (var i = 1; i <= 400; i++)
        {
            // ~4% of rows are the fault: hot and shaking while flow drops away.
            var faulty = random.NextDouble() < 0.04;

            var load = 0.6 + (random.NextDouble() * 0.4);
            var pressure = Math.Round(1800 + (load * 600) + Noise(random, 25), 1);
            var temperature = Math.Round((faulty ? 96 : 71) + (load * 12) + Noise(random, 2.5), 1);
            var vibration = Math.Round((faulty ? 5.8 : 1.9) + Noise(random, 0.35), 2);
            var flow = Math.Round((faulty ? 480 : 1250) * load + Noise(random, 40), 1);
            var current = Math.Round((faulty ? 38 : 54) + (load * 9) + Noise(random, 1.8), 1);

            rows.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{i},{pressure},{temperature},{vibration},{flow},{current},{(faulty ? 1 : 0)}\n");
        }

        return rows.ToString();
    }

    /// <summary>Box–Muller, so the sample looks like readings rather than a uniform smear.</summary>
    private static double Noise(Random random, double sigma)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
