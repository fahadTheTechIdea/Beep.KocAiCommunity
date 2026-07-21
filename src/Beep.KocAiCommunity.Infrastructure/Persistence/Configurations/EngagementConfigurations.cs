using Beep.KocAiCommunity.Domain.Engagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfiles", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.CompanyId).HasMaxLength(32);
        b.Property(x => x.DepartmentId).HasMaxLength(32);
        b.Property(x => x.Bio).HasMaxLength(280);
        b.Property(x => x.AvatarIcon).HasMaxLength(128).IsRequired();
        b.Property(x => x.SkillsCsv).HasMaxLength(512);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.Email);           // uniqueness enforced in the admin service
        b.HasIndex(x => x.OrgUnitId);
        b.HasIndex(x => x.XpTotal);
    }
}

public sealed class XpEventConfiguration : IEntityTypeConfiguration<XpEvent>
{
    public void Configure(EntityTypeBuilder<XpEvent> b)
    {
        b.ToTable("XpEvents", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Source).HasMaxLength(64).IsRequired();
        b.Property(x => x.RefType).HasMaxLength(32);
        b.HasIndex(x => new { x.UserId, x.Source, x.RefId });
        b.HasIndex(x => x.CreatedUtc);
    }
}

public sealed class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> b)
    {
        b.ToTable("Badges", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(512).IsRequired();
        b.Property(x => x.IconFile).HasMaxLength(128).IsRequired();
        b.Property(x => x.Tier).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class UserBadgeConfiguration : IEntityTypeConfiguration<UserBadge>
{
    public void Configure(EntityTypeBuilder<UserBadge> b)
    {
        b.ToTable("UserBadges", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.BadgeCode).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.UserId, x.BadgeCode }).IsUnique();
    }
}

public sealed class KudosConfiguration : IEntityTypeConfiguration<Kudos>
{
    public void Configure(EntityTypeBuilder<Kudos> b)
    {
        b.ToTable("Kudos", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.ToUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Message).HasMaxLength(200).IsRequired();
        b.Property(x => x.Emoji).HasMaxLength(16).IsRequired();
        b.Property(x => x.RefType).HasMaxLength(32);
        b.HasIndex(x => new { x.ToUserId, x.CreatedUtc });
        b.HasIndex(x => new { x.FromUserId, x.CreatedUtc });
    }
}

public sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> b)
    {
        b.ToTable("ActivityEvents", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Type).HasMaxLength(64).IsRequired();
        b.Property(x => x.RefType).HasMaxLength(32);
        b.HasIndex(x => new { x.VisibilityOrgUnitId, x.CreatedUtc });
        b.HasIndex(x => x.CreatedUtc);
    }
}
