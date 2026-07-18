using Beep.KocAiCommunity.Domain.Audit;
using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> b)
    {
        b.ToTable("OrgUnits", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Path).HasMaxLength(1024).IsRequired();
        b.Property(x => x.LeaderUserId).HasMaxLength(450);
        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => x.Path);
        b.HasIndex(x => new { x.Type, x.LeaderUserId });
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrgMembershipConfiguration : IEntityTypeConfiguration<OrgMembership>
{
    public void Configure(EntityTypeBuilder<OrgMembership> b)
    {
        b.ToTable("OrgMemberships", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.OrgUnitId);
        // Lookups by user; the single-primary (home) invariant is enforced by the org directory.
        b.HasIndex(x => new { x.UserId, x.IsPrimary });
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserEntityPermissionConfiguration : IEntityTypeConfiguration<UserEntityPermission>
{
    public void Configure(EntityTypeBuilder<UserEntityPermission> b)
    {
        b.ToTable("UserEntityPermissions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.ResourceType).HasMaxLength(64).IsRequired();
        b.Property(x => x.PermissionKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.UserId, x.ResourceType, x.ResourceId });
        b.HasIndex(x => new { x.UserId, x.ResourceType, x.ResourceId, x.PermissionKey }).IsUnique();
    }
}

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.ToTable("AdminAuditLog", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Action).HasMaxLength(128).IsRequired();
        b.Property(x => x.Resource).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.OccurredUtc);
        b.HasIndex(x => new { x.ActorUserId, x.OccurredUtc });
    }
}
