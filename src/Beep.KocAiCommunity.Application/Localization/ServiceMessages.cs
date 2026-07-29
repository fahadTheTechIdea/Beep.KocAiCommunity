using System.Globalization;

namespace Beep.KocAiCommunity.Application.Localization;

/// <summary>
/// Marker for the service-message resource — the sentences a member reads when something they tried
/// could not be done: a quota reached, a discussion locked, a version that needs another approval.
/// <para>
/// Separate from the interface resource because of where it can be referenced from. Interface strings
/// live in Ui.Shared; the services that raise these live in Infrastructure, which has no business
/// referencing a UI project. This one sits in Application, which Infrastructure already depends on.
/// </para>
/// <para>
/// Same convention as the interface: the English sentence is the key, so an untranslated message reads
/// as correct English rather than an identifier.
/// </para>
/// </summary>
public sealed class ServiceMessages;

/// <summary>
/// A message a member is meant to read, kept as a template plus its values rather than a finished
/// sentence.
/// <para>
/// A message built by interpolation — <c>$"Quota ({n}) reached."</c> — is a different string for every
/// n, so it can never be looked up in a resource. Keeping the template separate means the English
/// stays a stable key and the numbers are filled in after translation, in whichever language.
/// </para>
/// </summary>
public interface IUserFacingMessage
{
    /// <summary>The English sentence with <c>{0}</c>-style placeholders — the resource key.</summary>
    string Template { get; }

    /// <summary>The values for the placeholders, empty when the template has none.</summary>
    object[] TemplateArgs { get; }
}

/// <summary>Shared formatting so every exception type builds its English the same way.</summary>
public static class UserFacingMessage
{
    public static string Format(string template, object[] args) =>
        args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
}
