using Beep.KocAiCommunity.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class DiscussionConfiguration : IEntityTypeConfiguration<Discussion>
{
    public void Configure(EntityTypeBuilder<Discussion> b)
    {
        b.ToTable("Discussions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Body).HasMaxLength(8192).IsRequired();
        b.Property(x => x.AuthorUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.VisibilityScope, x.VisibilityOrgUnitId });
    }
}

public sealed class DiscussionReplyConfiguration : IEntityTypeConfiguration<DiscussionReply>
{
    public void Configure(EntityTypeBuilder<DiscussionReply> b)
    {
        b.ToTable("DiscussionReplies", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.AuthorUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Body).HasMaxLength(8192).IsRequired();
        b.HasIndex(x => new { x.DiscussionId, x.CreatedUtc });
        b.HasOne<Discussion>().WithMany().HasForeignKey(x => x.DiscussionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> b)
    {
        b.ToTable("Reactions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.TargetType).HasMaxLength(16).IsRequired();
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Emoji).HasMaxLength(16).IsRequired();
        // One reaction per (target, user, emoji) — reacting again toggles it off.
        b.HasIndex(x => new { x.TargetType, x.TargetId, x.UserId, x.Emoji }).IsUnique();
        b.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}

public sealed class MentionConfiguration : IEntityTypeConfiguration<Mention>
{
    public void Configure(EntityTypeBuilder<Mention> b)
    {
        b.ToTable("Mentions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceType).HasMaxLength(16).IsRequired();
        b.Property(x => x.MentionedUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.ByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.MentionedUserId, x.CreatedUtc });
    }
}

public sealed class DiscussionAttachmentConfiguration : IEntityTypeConfiguration<DiscussionAttachment>
{
    public void Configure(EntityTypeBuilder<DiscussionAttachment> b)
    {
        b.ToTable("DiscussionAttachments", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        b.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.DiscussionId);
        b.HasOne<Discussion>().WithMany().HasForeignKey(x => x.DiscussionId).OnDelete(DeleteBehavior.Cascade);
    }
}
