namespace Beep.KocAiCommunity.Ui.Shared.Localization;

/// <summary>
/// Marker type for the shared string resource — inject <c>IStringLocalizer&lt;Strings&gt;</c> as <c>L</c>
/// and index it with the English text: <c>@L["Open"]</c>.
/// <para>
/// One resource for the whole interface rather than one per component. "Save", "Cancel", and "Open"
/// appear on a dozen pages; per-component resources would make a translator translate each of them a
/// dozen times, and let the twelve copies disagree.
/// </para>
/// <para>
/// The English text <b>is</b> the key. <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/>
/// returns the key when it finds no translation, so a string that has not been added to the Arabic
/// resource yet still renders correct English rather than a raw identifier. That is what lets this roll
/// out across sixty-odd files without a broken state in between — and the resource-coverage test is
/// what stops "renders English" from quietly becoming the permanent answer.
/// </para>
/// <para>
/// Resource lookup relies on this type sitting beside <c>Strings.resx</c> in the same folder and
/// namespace, with no <c>ResourcesPath</c> configured. Moving one without the other silently falls back
/// to English everywhere.
/// </para>
/// </summary>
public sealed class Strings;
