namespace Beep.KocAiCommunity.Client;

/// <summary>A selectable "view as" persona mapping to a dev user id and its roles.</summary>
public sealed record Persona(string Key, string Label, string? UserId, IReadOnlyList<string> Roles)
{
    public bool IsGuest => UserId is null;
}

/// <summary>
/// The dev identity the Web acts as when calling the API without a real Entra tenant. Forwarded to
/// the API via headers by <see cref="DevIdentityHandler"/>. In production this is replaced by real
/// Entra token forwarding. Switching persona is reactive so the nav and route guard update live.
/// </summary>
public sealed class DevIdentity
{
    // Position levels (one per user) plus additive function roles — mirrors KocRoles on the server.
    public static readonly IReadOnlyList<Persona> Personas =
    [
        new("guest", "Guest (not signed in)", null, []),
        new("employee", "Employee", "dev-emp-1", ["Employee"]),
        new("teamleader", "Team Leader", "dev-lead", ["TeamLeader"]),
        new("manager", "Manager", "dev-user", ["Manager"]),
        new("dceo", "DCEO", "dev-dceo", ["DCEO"]),
        new("ceo", "CEO", "dev-ceo", ["CEO"]),
        new("compadmin", "Competition Admin", "dev-compadmin", ["Employee", "CompetitionAdmin"]),
        new("platformadmin", "Platform Admin", "dev-admin", ["Manager", "PlatformAdmin"]),
    ];

    private static readonly string[] Positions = ["Employee", "TeamLeader", "Manager", "DCEO", "CEO"];
    private static readonly string[] SupervisorRoles = ["TeamLeader", "Manager", "DCEO", "CEO", "PlatformAdmin"];

    // Default to Platform Admin so every area — including Admin — is visible out of the box.
    // (Switch persona from the app bar to view the app as any other role.)
    private Persona _persona = Personas.Single(p => p.Key == "platformadmin");

    /// <summary>Raised when the persona changes so nav + guards can re-render.</summary>
    public event Action? Changed;

    public Persona Current => _persona;
    public string UserId { get; set; } = "dev-admin";
    public IReadOnlyList<string> Roles => _persona.Roles;

    public bool IsGuest => _persona.IsGuest;
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyPosition => Roles.Any(r => Positions.Contains(r, StringComparer.OrdinalIgnoreCase));
    public bool IsSupervisor => Roles.Any(r => SupervisorRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
    public bool IsPlatformAdmin => IsInRole("PlatformAdmin");

    public void SetPersona(string key)
    {
        var next = Personas.FirstOrDefault(p => p.Key == key);
        if (next is null || next.Key == _persona.Key)
        {
            return;
        }

        _persona = next;
        UserId = next.UserId ?? string.Empty;
        Changed?.Invoke();
    }

    /// <summary>The synthetic persona key that stands for the real, environment-signed-in user.</summary>
    public const string RealUserKey = "__me";

    /// <summary>True when acting as the real signed-in user rather than a dev persona.</summary>
    public bool IsRealUser => _persona.Key == RealUserKey;

    /// <summary>
    /// Acts as the real user resolved from the host environment (e.g. the Windows/Entra account on the
    /// desktop) instead of a seeded dev persona. Roles default to Employee when unknown.
    /// </summary>
    public void SetRealUser(string userId, string displayName, IReadOnlyList<string> roles)
    {
        _persona = new Persona(RealUserKey, displayName, userId, roles.Count > 0 ? roles : ["Employee"]);
        UserId = userId;
        Changed?.Invoke();
    }
}

/// <summary>
/// Whether the persona headers may be sent at all. Off once a real sign-in mode is configured: those
/// headers are an assertion the API only honours in demo mode, and sending them elsewhere is at best
/// noise and at worst a habit worth not forming.
/// </summary>
public sealed record DevIdentityOptions(bool ForwardHeaders = true);

/// <summary>Attaches the current dev identity to outgoing API requests (nothing for a guest).</summary>
public sealed class DevIdentityHandler(DevIdentity identity, DevIdentityOptions options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Dev-User");
        request.Headers.Remove("X-Dev-Roles");

        // A guest sends no identity, so the API's dev auth treats the call as unauthenticated.
        if (options.ForwardHeaders && !identity.IsGuest && !string.IsNullOrEmpty(identity.UserId))
        {
            request.Headers.Add("X-Dev-User", identity.UserId);
            request.Headers.Add("X-Dev-Roles", string.Join(',', identity.Roles));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
