using Microsoft.AspNetCore.Components.Forms;

namespace Beep.KocAiCommunity.Web.Services;

/// <summary>
/// Writes competition hero images into the web app's <c>wwwroot</c> so they are served as ordinary
/// static files (same origin, no auth), and returns the web-relative path to persist via the API.
/// </summary>
public sealed class HeroImageStorage(IWebHostEnvironment env)
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    /// <summary>Saves the uploaded image under <c>wwwroot/uploads/competitions</c> and returns its web path.</summary>
    public async Task<string> SaveAsync(Guid competitionId, IBrowserFile file, CancellationToken ct = default)
    {
        if (!Extensions.TryGetValue(file.ContentType, out var ext))
        {
            throw new InvalidOperationException("The hero image must be a PNG, JPG, WEBP, or GIF.");
        }

        var dir = Path.Combine(env.WebRootPath, "uploads", "competitions");
        Directory.CreateDirectory(dir);

        // Drop any prior image for this competition first (a different extension would otherwise be orphaned).
        foreach (var old in Directory.EnumerateFiles(dir, competitionId + ".*"))
        {
            try { File.Delete(old); } catch { /* best effort */ }
        }

        var fileName = $"{competitionId}{ext}";
        await using (var target = File.Create(Path.Combine(dir, fileName)))
        await using (var source = file.OpenReadStream(MaxBytes, ct))
        {
            await source.CopyToAsync(target, ct);
        }

        return $"/uploads/competitions/{fileName}";
    }
}
