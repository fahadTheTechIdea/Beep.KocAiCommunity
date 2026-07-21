using Beep.KocAiCommunity.Client;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>
/// Default provider: the user is already signed in to the intranet (Windows/Entra), so this resolves
/// their account silently — no extra login. Department + profile info are left null here and are meant
/// to be filled by a KOC directory-API provider (wired in later) that enriches this result.
/// </summary>
public sealed class WindowsEnvironmentUserProvider : IEnvironmentUserProvider
{
    public Task<EnvironmentUser> GetCurrentAsync(CancellationToken ct = default)
    {
        var (userId, displayName) = WindowsUser.Current();
        return Task.FromResult(new EnvironmentUser(userId, displayName));
    }
}
