using Beep.KocAiCommunity.Application.Localization;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Domain.Localization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Localization;

/// <inheritdoc />
public sealed class ContentTranslator(KocDbContext db) : IContentTranslator
{
    public async Task<IReadOnlyDictionary<string, string>> LookupAsync(
        string entityType, string field, string language, CancellationToken ct = default)
    {
        var wanted = KocLanguages.Normalize(language);

        // English is what the rows already hold, so there is nothing to look up and no reason to ask.
        if (wanted == KocLanguages.English)
        {
            return new Dictionary<string, string>();
        }

        var rows = await db.Set<ContentTranslation>().AsNoTracking()
            .Where(t => t.EntityType == entityType && t.Field == field && t.Language == wanted)
            .Select(t => new { t.EntityKey, t.Text })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.EntityKey, r => r.Text, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> TranslateAsync(
        string entityType, string entityKey, string field, string language, string fallback, CancellationToken ct = default)
    {
        var lookup = await LookupAsync(entityType, field, language, ct);
        return lookup.TryGetValue(entityKey, out var text) && !string.IsNullOrWhiteSpace(text) ? text : fallback;
    }
}
