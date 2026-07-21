namespace Beep.KocAiCommunity.Client;

/// <summary>
/// The signed-in user resolved from the host environment. Identity comes from the already-authenticated
/// intranet session (no extra login); <see cref="Email"/>/<see cref="CompanyId"/>/<see cref="DepartmentId"/>/
/// <see cref="Roles"/> are enriched from the KOC directory API when one is wired in (null until then).
/// </summary>
public sealed record EnvironmentUser(
    string UserId,
    string DisplayName,
    string? Email = null,
    string? CompanyId = null,
    string? DepartmentId = null,
    IReadOnlyList<string>? Roles = null);

/// <summary>
/// Resolves the current signed-in user. The default implementation reads the intranet/Windows session
/// silently; a directory-API implementation can replace it later to fill in department and profile info.
/// </summary>
public interface IEnvironmentUserProvider
{
    Task<EnvironmentUser> GetCurrentAsync(CancellationToken ct = default);
}

/// <summary>Holds the resolved signed-in user for the app session (populated once at startup).</summary>
public sealed class SignedInUser
{
    public EnvironmentUser? Current { get; set; }
}
