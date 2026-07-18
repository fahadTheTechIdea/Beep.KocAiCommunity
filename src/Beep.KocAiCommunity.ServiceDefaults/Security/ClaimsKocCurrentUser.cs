using System.Security.Claims;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Organization;
using Microsoft.AspNetCore.Http;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// <see cref="IKocCurrentUser"/> backed by the request's Entra claims. Position level is derived
/// from role claims; home/led org unit ids are read from custom claims populated at sign-in.
/// </summary>
public sealed class ClaimsKocCurrentUser(IHttpContextAccessor accessor) : IKocCurrentUser
{
    private const string HomeUnitClaim = "koc_home_unit";
    private const string LedUnitClaim = "koc_led_unit";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId =>
        Principal?.FindFirstValue("oid")
        ?? Principal?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? DisplayName =>
        Principal?.FindFirstValue("name") ?? Principal?.Identity?.Name;

    public IReadOnlyCollection<string> Roles =>
        Principal is null
            ? []
            : Principal.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(Principal.FindAll("roles").Select(c => c.Value))
                .Distinct()
                .ToArray();

    public PositionLevel PositionLevel
    {
        get
        {
            if (IsInRole(KocRoles.CEO)) return PositionLevel.CEO;
            if (IsInRole(KocRoles.DCEO)) return PositionLevel.DCEO;
            if (IsInRole(KocRoles.Manager)) return PositionLevel.Manager;
            if (IsInRole(KocRoles.TeamLeader)) return PositionLevel.TeamLeader;
            return PositionLevel.Employee;
        }
    }

    public Guid? HomeOrgUnitId => ParseGuid(Principal?.FindFirstValue(HomeUnitClaim));
    public Guid? LedOrgUnitId => ParseGuid(Principal?.FindFirstValue(LedUnitClaim));

    public bool IsInRole(string role) => Roles.Contains(role);

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var g) ? g : null;
}
