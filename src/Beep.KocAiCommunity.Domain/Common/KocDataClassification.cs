namespace Beep.KocAiCommunity.Domain.Common;

/// <summary>
/// KOC information-security classification, ordered from least to most sensitive. Enforced on
/// artifact download and carried by datasets, projects, workflows, models, and competitions.
/// </summary>
public enum KocDataClassification
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3,
}
