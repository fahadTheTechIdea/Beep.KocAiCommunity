using Beep.KocAiCommunity.Application.Abstractions;
using Beep.KocAiCommunity.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.ArchitectureTests;

/// <summary>
/// Locks the dependency direction of the solution. Domain and Application stay free of
/// persistence and framework concerns; those live only in Infrastructure and the hosts.
/// </summary>
public class DependencyDirectionTests
{
    private static readonly string[] ForbiddenForDomainAndApplication =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "MudBlazor",
        "Microsoft.ML",
    ];

    [Fact]
    public void Domain_does_not_reference_persistence_ui_or_ml()
    {
        var referenced = typeof(AuditableEntity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();

        referenced.Should().NotContain(ForbiddenForDomainAndApplication);
    }

    [Fact]
    public void Application_does_not_reference_persistence_ui_or_ml()
    {
        var referenced = typeof(IArtifactStore).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();

        referenced.Should().NotContain(ForbiddenForDomainAndApplication);
    }
}
