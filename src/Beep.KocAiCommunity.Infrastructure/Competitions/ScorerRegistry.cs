using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>Resolves trusted scorers registered in DI by their code.</summary>
public sealed class ScorerRegistry : IScorerRegistry
{
    private readonly Dictionary<string, IScoringPlugin> _byCode;

    public ScorerRegistry(IEnumerable<IScoringPlugin> plugins) =>
        _byCode = plugins.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

    public IScoringPlugin Resolve(string scorerCode) =>
        _byCode.TryGetValue(scorerCode, out var plugin)
            ? plugin
            : throw new CompetitionException("No trusted scorer registered for '{0}'.", scorerCode);
}
