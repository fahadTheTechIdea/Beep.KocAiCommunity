namespace Beep.KocAiCommunity.Application.Storage;

/// <summary>Upload guards for the artifact service. Bound from configuration section <c>Artifacts</c>.</summary>
public sealed class ArtifactUploadOptions
{
    public const string SectionName = "Artifacts";

    /// <summary>Root path for the local filesystem store (dev/test).</summary>
    public string RootPath { get; set; } = "artifacts";

    /// <summary>Maximum accepted upload size. Default 200 MB.</summary>
    public long MaxSizeBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>Lower-cased extensions (with dot) that are accepted. Empty = allow any.</summary>
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
