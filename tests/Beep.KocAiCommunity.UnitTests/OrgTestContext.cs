using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Builds a KocDbContext over an in-memory SQLite connection and seeds a small KOC org tree:
///
///   /koc (Company, CEO=ceo)
///     /koc/d1 (Directorate, DCEO=dceo1)
///       /koc/d1/g1 (Group, Manager=mgr1)
///         /koc/d1/g1/t1 (Team, Leader=lead1)  -> member emp1
///         /koc/d1/g1/t2 (Team)                -> member emp2
///     /koc/d2 (Directorate)
///       /koc/d2/t9 (Team)                     -> member empOther
/// </summary>
public sealed class OrgTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    public KocDbContext Db { get; }

    public Guid Company { get; private set; }
    public Guid D1 { get; private set; }
    public Guid G1 { get; private set; }
    public Guid T1 { get; private set; }
    public Guid T2 { get; private set; }
    public Guid D2 { get; private set; }
    public Guid T9 { get; private set; }

    public OrgTestContext()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<KocDbContext>().UseSqlite(_connection).Options;
        Db = new KocDbContext(options);
        Db.Database.EnsureCreated();
        Seed();
    }

    private void Seed()
    {
        var company = Unit("KOC", OrgUnitType.Company, null, "/koc", leader: "ceo");
        var d1 = Unit("Exploration", OrgUnitType.Directorate, company.Id, "/koc/d1", leader: "dceo1");
        var g1 = Unit("Subsurface", OrgUnitType.Group, d1.Id, "/koc/d1/g1", leader: "mgr1");
        var t1 = Unit("Reservoir Analytics", OrgUnitType.Team, g1.Id, "/koc/d1/g1/t1", leader: "lead1");
        var t2 = Unit("Production Eng", OrgUnitType.Team, g1.Id, "/koc/d1/g1/t2");
        var d2 = Unit("Operations", OrgUnitType.Directorate, company.Id, "/koc/d2");
        var t9 = Unit("Facilities", OrgUnitType.Team, d2.Id, "/koc/d2/t9");

        Db.OrgUnits.AddRange(company, d1, g1, t1, t2, d2, t9);
        Db.OrgMemberships.AddRange(
            Member("emp1", t1.Id, PositionLevel.Employee),
            Member("emp2", t2.Id, PositionLevel.Employee),
            Member("empOther", t9.Id, PositionLevel.Employee),
            Member("lead1", t1.Id, PositionLevel.TeamLeader),
            Member("mgr1", g1.Id, PositionLevel.Manager));
        Db.SaveChanges();

        Company = company.Id; D1 = d1.Id; G1 = g1.Id; T1 = t1.Id; T2 = t2.Id; D2 = d2.Id; T9 = t9.Id;
    }

    private static OrgUnit Unit(string name, OrgUnitType type, Guid? parent, string path, string? leader = null) => new()
    {
        Name = name,
        Type = type,
        ParentId = parent,
        Path = path,
        LeaderUserId = leader,
        CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static OrgMembership Member(string userId, Guid orgUnitId, PositionLevel level) => new()
    {
        UserId = userId,
        OrgUnitId = orgUnitId,
        PositionLevel = level,
        IsPrimary = true,
        FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
