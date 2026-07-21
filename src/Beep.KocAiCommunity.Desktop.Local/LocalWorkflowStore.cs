using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>A stored workflow version (the graph JSON + metadata).</summary>
public sealed class StoredVersion
{
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft";      // draft | published | archived
    public int SchemaVersion { get; set; } = 1;
    public string DefinitionJson { get; set; } = "{}";
    public string SnapshotHash { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>A stored workflow: the local equivalent of a registry row + its versions.</summary>
public sealed class StoredWorkflow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Classification { get; set; } = "Internal";
    public string OwnerUserId { get; set; } = "local";
    public DateTime CreatedUtc { get; set; }
    public Guid? CompetitionId { get; set; }
    public List<StoredVersion> Versions { get; set; } = [];

    public StoredVersion Latest => Versions.OrderByDescending(v => v.VersionNumber).First();
}

/// <summary>File-backed workflow registry: one <c>{guid}.json</c> per workflow under <c>workflows/</c>.</summary>
public sealed class LocalWorkflowStore(LocalWorkspace workspace)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _gate = new();

    public IReadOnlyList<StoredWorkflow> All()
    {
        lock (_gate)
        {
            return Directory.EnumerateFiles(workspace.WorkflowsPath, "*.json")
                .Select(Read).Where(w => w is not null).Select(w => w!)
                .OrderByDescending(w => w.CreatedUtc).ToList();
        }
    }

    public StoredWorkflow? Get(Guid id)
    {
        lock (_gate)
        {
            return Read(PathFor(id));
        }
    }

    public StoredWorkflow Create(string name, string description, string classification, Guid? competitionId)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            var wf = new StoredWorkflow
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "New workflow" : name.Trim(),
                Description = description,
                Classification = classification,
                CreatedUtc = now,
                CompetitionId = competitionId,
                Versions = [new StoredVersion { VersionNumber = 1, Status = "draft", DefinitionJson = "{}", SnapshotHash = Hash("{}"), CreatedUtc = now }],
            };
            Write(wf);
            return wf;
        }
    }

    /// <summary>Overwrites the current draft, or opens a new draft if the latest is frozen.</summary>
    public StoredVersion SaveDraft(Guid id, string definitionJson, string? notes)
    {
        lock (_gate)
        {
            var wf = Read(PathFor(id)) ?? throw new InvalidOperationException("Workflow not found.");
            var latest = wf.Latest;
            StoredVersion target;
            if (latest.Status == "draft")
            {
                target = latest;
            }
            else
            {
                target = new StoredVersion { VersionNumber = latest.VersionNumber + 1, Status = "draft", CreatedUtc = DateTime.UtcNow };
                wf.Versions.Add(target);
            }

            target.DefinitionJson = definitionJson;
            target.Notes = notes;
            target.SnapshotHash = Hash(definitionJson);
            Write(wf);
            return target;
        }
    }

    public string? SetStatus(Guid id, int versionNumber, string status)
    {
        lock (_gate)
        {
            var wf = Read(PathFor(id));
            var version = wf?.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
            if (wf is null || version is null)
            {
                return "Version not found.";
            }

            version.Status = status;
            version.PublishedUtc = status == "published" ? DateTime.UtcNow : version.PublishedUtc;
            Write(wf);
            return null;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            var path = PathFor(id);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }

    public StoredWorkflow Import(string name, string envelopeJson)
    {
        // The local export envelope is just the definition JSON; import creates a fresh draft from it.
        var wf = Create(name, "", "Internal", null);
        SaveDraft(wf.Id, envelopeJson, "imported");
        return Get(wf.Id)!;
    }

    private string PathFor(Guid id) => Path.Combine(workspace.WorkflowsPath, $"{id:N}.json");

    private static StoredWorkflow? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredWorkflow>(File.ReadAllText(path), Json);
        }
        catch
        {
            return null;
        }
    }

    private void Write(StoredWorkflow wf) =>
        File.WriteAllText(PathFor(wf.Id), JsonSerializer.Serialize(wf, Json));

    private static string Hash(string s) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
