using System.Security.Principal;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>Resolves the real signed-in user from the Windows/Entra environment (no app registration).</summary>
internal static class WindowsUser
{
    /// <summary>
    /// The current Windows/Entra account. <c>UserId</c> is the full account name (e.g. <c>KOC\aldhubaib</c>
    /// or <c>AzureAD\First Last</c>) used to attribute work; <c>DisplayName</c> drops the domain prefix.
    /// </summary>
    public static (string UserId, string DisplayName) Current()
    {
        string name;
        try
        {
            name = WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            name = Environment.UserName;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Environment.UserName;
        }

        var slash = name.IndexOf('\\');
        var display = slash >= 0 ? name[(slash + 1)..] : name;
        return (name, display);
    }
}
