using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Application.Connectors;

/// <summary>Supported connector authentication modes.</summary>
public enum ConnectorAuthMode { None, Basic, OAuth2ClientCredentials, Certificate, Integrated, SharedKey }

/// <summary>What a connector can do.</summary>
public sealed record ConnectorCapabilities(bool Schema, bool Browse, bool Import, bool ReadOnly);

/// <summary>
/// A code-first description of one KOC enterprise connector: its identity, default information-security
/// classification, allowed auth modes, and capabilities. The set of connectors lives in source; instances
/// (endpoints + credentials) live in the database.
/// </summary>
public sealed record ConnectorDescriptor(
    string Code, string DisplayName, string Version, string Description,
    KocDataClassification DefaultClassification, IReadOnlyList<ConnectorAuthMode> AuthModes, ConnectorCapabilities Capabilities);

/// <summary>The KOC enterprise connector catalog (PPDM, OpenWells, EcoSys, SAP, PI, ADLS Gen2).</summary>
public static class ConnectorCatalog
{
    private static ConnectorCapabilities Cap(bool import = true) => new(Schema: true, Browse: true, Import: import, ReadOnly: true);

    public static readonly IReadOnlyList<ConnectorDescriptor> All =
    [
        new("ppdm", "PPDM 39", "39", "Public Petroleum Data Model — wells, wellbores, logs, production (read-only SQL).",
            KocDataClassification.Confidential, [ConnectorAuthMode.Basic, ConnectorAuthMode.Integrated], Cap()),
        new("openwells", "OpenWells", "2023.1", "OpenWells Activity API — drilling and completion activities.",
            KocDataClassification.Confidential, [ConnectorAuthMode.OAuth2ClientCredentials, ConnectorAuthMode.Basic], Cap()),
        new("ecosys", "EcoSys", "8.9", "EcoSys Project Server — projects, portfolios, schedules.",
            KocDataClassification.Internal, [ConnectorAuthMode.OAuth2ClientCredentials], Cap()),
        new("sap", "SAP PM/MM", "ECC6", "SAP RFC/BAPI gateway for Plant Maintenance and Materials (read-only).",
            KocDataClassification.Confidential, [ConnectorAuthMode.Basic, ConnectorAuthMode.Certificate], Cap()),
        new("pi", "AVEVA PI", "2018 SP3", "PI Web API — AF databases and tags; time-series pull.",
            KocDataClassification.Restricted, [ConnectorAuthMode.Basic, ConnectorAuthMode.Certificate], Cap()),
        new("adls", "ADLS Gen2", "gen2", "Azure Data Lake Storage Gen2 — containers and blobs.",
            KocDataClassification.Internal, [ConnectorAuthMode.SharedKey, ConnectorAuthMode.Certificate], Cap()),
    ];

    public static ConnectorDescriptor? Find(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
}
