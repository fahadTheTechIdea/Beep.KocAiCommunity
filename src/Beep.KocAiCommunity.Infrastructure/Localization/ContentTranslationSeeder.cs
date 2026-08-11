using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Domain.Localization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Localization;

/// <summary>
/// The Arabic for the content KOC ships with the platform.
/// <para>
/// Idempotent per entry and never overwriting: once a row exists it is left alone, the same bargain the
/// category seeder makes about names. If somebody corrects a translation in the database, a later
/// release must not quietly put mine back.
/// </para>
/// <para>
/// Only seeded content appears here. A competition a colleague hosts keeps the title they gave it.
/// </para>
/// </summary>
public static class ContentTranslationSeeder
{
    /// <summary>(entity type, key, field, text) — the Arabic for every category and badge KOC seeds.</summary>
    private static readonly (string Type, string Key, string Field, string Text)[] Arabic =
    [
        // ---- competition categories: the KOC operational domains ----
        (TranslatedContent.CompetitionCategory, "subsurface", TranslatedContent.Name, "المكامن"),
        (TranslatedContent.CompetitionCategory, "subsurface", TranslatedContent.Description,
            "توصيف المكامن، والجيولوجيا، والفيزياء الصخرية، وسجلات الآبار."),
        (TranslatedContent.CompetitionCategory, "drilling-wells", TranslatedContent.Name, "الحفر والآبار"),
        (TranslatedContent.CompetitionCategory, "drilling-wells", TranslatedContent.Description,
            "معدّل الاختراق، والوقت غير المنتج، وسلامة الآبار."),
        (TranslatedContent.CompetitionCategory, "production", TranslatedContent.Name, "الإنتاج"),
        (TranslatedContent.CompetitionCategory, "production", TranslatedContent.Description,
            "المعدّلات، والانحدار، والرفع الصناعي، وتحسين الإنتاج."),
        (TranslatedContent.CompetitionCategory, "facilities", TranslatedContent.Name, "المرافق والمصانع"),
        (TranslatedContent.CompetitionCategory, "facilities", TranslatedContent.Description,
            "أداء العمليات، واستهلاك الطاقة، والطاقة الإنتاجية للمصنع."),
        // HSE stays HSE — it is the department's name, not a phrase to translate.
        (TranslatedContent.CompetitionCategory, "hse", TranslatedContent.Name, "HSE"),
        (TranslatedContent.CompetitionCategory, "hse", TranslatedContent.Description,
            "الحوادث، وحالات الوشك، والسلوك غير الطبيعي للحساسات."),
        (TranslatedContent.CompetitionCategory, "maintenance", TranslatedContent.Name, "الصيانة والموثوقية"),
        (TranslatedContent.CompetitionCategory, "maintenance", TranslatedContent.Description,
            "التنبؤ بالأعطال، والعمر التشغيلي المتبقي، وقطع الغيار."),
        (TranslatedContent.CompetitionCategory, "medical", TranslatedContent.Name, "الطب والصحة"),
        (TranslatedContent.CompetitionCategory, "medical", TranslatedContent.Description,
            "مستشفى الأحمدي: الفحوصات المخبرية، والتشخيص، وحركة المرضى، والصحة المهنية."),
        (TranslatedContent.CompetitionCategory, "training", TranslatedContent.Name, "التدريب والتطوير"),
        (TranslatedContent.CompetitionCategory, "training", TranslatedContent.Description,
            "التدريب وتطوير المسار الوظيفي: الطلب على الدورات، والحضور، وإتمامها، والزمن اللازم للكفاءة."),
        (TranslatedContent.CompetitionCategory, "people", TranslatedContent.Name, "الموارد البشرية"),
        (TranslatedContent.CompetitionCategory, "people", TranslatedContent.Description,
            "سجلات القوى العاملة، وسلامة الرواتب، وتوفير الكوادر، وتخطيط الغياب."),
    ];

    public static async Task SeedAsync(KocDbContext db, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var existing = await db.ContentTranslations
            .Where(t => t.Language == KocLanguages.Arabic)
            .Select(t => new { t.EntityType, t.EntityKey, t.Field })
            .ToListAsync(ct);

        var have = existing
            .Select(e => $"{e.EntityType}|{e.EntityKey}|{e.Field}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var (type, key, field, text) in Arabic.Concat(BadgeArabic()))
        {
            if (have.Contains($"{type}|{key}|{field}"))
            {
                continue;
            }

            db.ContentTranslations.Add(new ContentTranslation
            {
                EntityType = type,
                EntityKey = key,
                Field = field,
                Language = KocLanguages.Arabic,
                Text = text,
                CreatedUtc = stamp,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// The badges, keyed on <see cref="Engagement.BadgeCatalog"/>'s codes.
    /// <para>
    /// The English names are oilfield metaphors — Wildcatter, Gusher, Steady Pump — and translating them
    /// word for word would produce something no one would say. These keep the industry image instead:
    /// a gusher is فوّارة to anyone who works a field, and Conversation Driller keeps its drilling pun.
    /// Worth a native reviewer's eye more than the plain labels are.
    /// </para>
    /// </summary>
    private static IEnumerable<(string Type, string Key, string Field, string Text)> BadgeArabic()
    {
        (string Code, string Name, string Description)[] badges =
        [
            (Engagement.BadgeCatalog.FirstBarrel, "أول برميل", "كسبت أول برميل لك على المنصة."),
            (Engagement.BadgeCatalog.FirstSubmission, "منقِّب", "أرسلت أول مشاركة لك في مسابقة."),
            (Engagement.BadgeCatalog.CompetitionWinner, "فوّارة", "حللت أولًا في مسابقة."),
            (Engagement.BadgeCatalog.Podium, "على منصة التتويج", "حللت ضمن أفضل ثلاثة في مسابقة."),
            (Engagement.BadgeCatalog.FirstTrack, "معتمَد", "أتممت أول مسار تعليمي لك."),
            (Engagement.BadgeCatalog.AllTracks, "مشغّل خبير", "أتممت كل المسارات التعليمية المنشورة."),
            (Engagement.BadgeCatalog.Streak7, "ضخّ ثابت", "حافظت على نشاط سبعة أيام متتالية."),
            (Engagement.BadgeCatalog.Streak30, "تدفّق لا يتوقف", "حافظت على نشاط ثلاثين يومًا متتالية."),
            (Engagement.BadgeCatalog.Helper10, "الجار الطيّب", "تلقّيت عشر تحيات من الزملاء."),
            (Engagement.BadgeCatalog.DiscussionStarter, "حفّار النقاش", "بدأت خمسة نقاشات."),
            (Engagement.BadgeCatalog.TeamPlayer, "روح الفريق", "كنت ضمن فريق فاز بتحدٍّ جماعي."),
            (Engagement.BadgeCatalog.QuizPerfect, "العلامة الكاملة", "حصلت على 100% في اختبار مسار."),
            (Engagement.BadgeCatalog.QuizFirstTime, "من المحاولة الأولى", "اجتزت اختبار مسار من المحاولة الأولى."),
        ];

        foreach (var (code, name, description) in badges)
        {
            yield return (TranslatedContent.Badge, code, TranslatedContent.Name, name);
            yield return (TranslatedContent.Badge, code, TranslatedContent.Description, description);
        }
    }
}
