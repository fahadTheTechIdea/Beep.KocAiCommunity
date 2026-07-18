namespace Beep.KocAiCommunity.Domain.Common;

/// <summary>
/// Base type for persisted entities. Carries audit and soft-delete metadata.
/// EF configuration lives in Infrastructure; this type stays persistence-agnostic.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public string? LastModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public byte[]? RowVersion { get; set; }
}
