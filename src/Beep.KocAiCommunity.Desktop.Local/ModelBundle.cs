using System.IO.Compression;
using System.Text.Json;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>What a bundle turned out to contain, or why it was refused.</summary>
public sealed record ModelBundleReadResult(
    LocalModelVersion? Manifest, byte[]? ModelBytes, string? Refusal)
{
    public bool Accepted => Manifest is not null && ModelBytes is not null;
}

/// <summary>
/// A model as a single file — how an engineer sends one to a colleague or moves one between machines.
/// <para>
/// A <c>.kocmodel</c> is a zip of the model and its manifest, nothing more. It is deliberately dull:
/// anything cleverer would be a format to maintain.
/// </para>
/// </summary>
public static class ModelBundle
{
    public const string Extension = ".kocmodel";

    private const string ManifestEntry = "model.json";
    private const string ModelEntry = "model.zip";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>A filename that says what it is without needing the file opened.</summary>
    public static string SuggestedFileName(LocalModelVersion model) =>
        $"{LocalModelStore.SafeName(model.Name)}-v{model.Version}{Extension}";

    public static async Task WriteAsync(
        string path, LocalModelVersion manifest, byte[] modelBytes, CancellationToken ct = default)
    {
        await using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        await using (var entry = archive.CreateEntry(ManifestEntry).Open())
        {
            await using var writer = new StreamWriter(entry);
            await writer.WriteAsync(JsonSerializer.Serialize(manifest, Json));
        }

        await using (var entry = archive.CreateEntry(ModelEntry).Open())
        {
            await entry.WriteAsync(modelBytes, ct);
        }
    }

    /// <summary>
    /// Reads a bundle, refusing anything this host cannot honestly load.
    /// <para>
    /// Every refusal names what is wrong. Loading an incompatible model anyway produces a crash a long
    /// way from its cause, which is the failure mode this check exists to avoid.
    /// </para>
    /// </summary>
    public static async Task<ModelBundleReadResult> ReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using var file = File.OpenRead(path);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry(ManifestEntry);
            var modelEntry = archive.GetEntry(ModelEntry);
            if (manifestEntry is null || modelEntry is null)
            {
                return new ModelBundleReadResult(null, null,
                    "This is not a KOC model file — it is missing the model or its details.");
            }

            LocalModelVersion? manifest;
            await using (var stream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<LocalModelVersion>(stream, Json, ct);
            }

            if (manifest is null)
            {
                return new ModelBundleReadResult(null, null, "The model's details could not be read.");
            }

            if (IsNewerThanHost(manifest.MlNetVersion))
            {
                return new ModelBundleReadResult(null, null,
                    $"This model was built with a newer version of KOC Studio (ML.NET {manifest.MlNetVersion}; "
                    + $"this machine has {LocalModelStore.CurrentMlNetVersion}). Update KOC Studio and try again.");
            }

            using var buffer = new MemoryStream();
            await using (var stream = modelEntry.Open())
            {
                await stream.CopyToAsync(buffer, ct);
            }

            return new ModelBundleReadResult(manifest, buffer.ToArray(), null);
        }
        catch (InvalidDataException)
        {
            return new ModelBundleReadResult(null, null, "This file is not a readable KOC model file.");
        }
        catch (Exception ex)
        {
            return new ModelBundleReadResult(null, null, ex.Message);
        }
    }

    /// <summary>
    /// True when the bundle came from a newer ML.NET than this host runs. An unparseable version is
    /// treated as acceptable — refusing on a malformed field would block a model that may load fine.
    /// </summary>
    private static bool IsNewerThanHost(string? bundleVersion) =>
        Version.TryParse(bundleVersion, out var bundle)
        && Version.TryParse(LocalModelStore.CurrentMlNetVersion, out var host)
        && bundle > host;
}
