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
