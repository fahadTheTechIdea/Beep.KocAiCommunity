namespace Beep.KocAiCommunity.Web.Services;

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

    // Default to Manager so the app is fully explorable out of the box.
    private Persona _persona = Personas.Single(p => p.Key == "manager");

    /// <summary>Raised when the persona changes so nav + guards can re-render.</summary>
    public event Action? Changed;

    public Persona Current => _persona;
    public string UserId { get; set; } = "dev-user";
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
}

/// <summary>Attaches the current dev identity to outgoing API requests (nothing for a guest).</summary>
public sealed class DevIdentityHandler(DevIdentity identity) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Dev-User");
        request.Headers.Remove("X-Dev-Roles");

        // A guest sends no identity, so the API's dev auth treats the call as unauthenticated.
        if (!identity.IsGuest && !string.IsNullOrEmpty(identity.UserId))
        {
            request.Headers.Add("X-Dev-User", identity.UserId);
            request.Headers.Add("X-Dev-Roles", string.Join(',', identity.Roles));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
