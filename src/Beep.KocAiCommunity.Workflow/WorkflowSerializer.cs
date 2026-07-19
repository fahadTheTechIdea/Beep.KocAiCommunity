using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Workflow;

/// <summary>
/// Parses, canonicalizes, and hashes <see cref="WorkflowDefinition"/> JSON. Canonicalization gives a
/// stable byte-for-byte form so a snapshot hash is deterministic and round-trips without data loss.
/// </summary>
public static class WorkflowSerializer
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    // Deterministic write: no indentation, stable property order (record declaration order).
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    /// <summary>Parses definition JSON, throwing <see cref="WorkflowSerializationException"/> on failure.</summary>
    public static WorkflowDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new WorkflowSerializationException("The workflow definition is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowDefinition>(json, ReadOptions)
                ?? throw new WorkflowSerializationException("The workflow definition is empty.");
        }
        catch (JsonException ex)
        {
            throw new WorkflowSerializationException($"The workflow definition is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>Re-serializes a definition to a stable, canonical JSON string.</summary>
    public static string Canonicalize(WorkflowDefinition definition) =>
        JsonSerializer.Serialize(definition, CanonicalOptions);

    /// <summary>Parses then canonicalizes — the form stored on a version.</summary>
    public static string Canonicalize(string json) => Canonicalize(Parse(json));

    /// <summary>A SHA-256 hex digest of the canonical form.</summary>
    public static string Hash(string json)
    {
        var canonical = Canonicalize(json);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>Raised when workflow definition JSON cannot be parsed.</summary>
public sealed class WorkflowSerializationException(string message) : Exception(message);
