namespace Beep.KocAiCommunity.Domain.Organization;

/// <summary>KOC org-tree node kind. Team ⊂ Group ⊂ Directorate ⊂ Company (KOC root).</summary>
public enum OrgUnitType
{
    Company = 0,
    Directorate = 1,
    Group = 2,
    Team = 3,
}

/// <summary>Position in the KOC reporting line.</summary>
public enum PositionLevel
{
    Employee = 0,
    TeamLeader = 1,
    Manager = 2,
    DCEO = 3,
    CEO = 4,
}

/// <summary>Who can see a competition, dataset, or project — chosen at creation.</summary>
public enum VisibilityScope
{
    Team = 0,
    Group = 1,
    Directorate = 2,
    Company = 3,
}
