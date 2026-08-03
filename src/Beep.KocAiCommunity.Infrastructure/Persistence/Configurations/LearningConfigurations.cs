using Beep.KocAiCommunity.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class LearningTrackConfiguration : IEntityTypeConfiguration<LearningTrack>
{
    public void Configure(EntityTypeBuilder<LearningTrack> b)
    {
        b.ToTable("LearningTracks", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(1024).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Domain).HasMaxLength(32).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired().HasDefaultValue(TrackLanguages.English);
        b.Property(x => x.ContentKey).HasMaxLength(64).IsRequired().HasDefaultValue(string.Empty);

        // The catalogue is always read for one language at a time, so it leads the index.
        b.HasIndex(x => new { x.Language, x.Status, x.OrderNo });

        // Finding a track's translations. Not unique: every unpaired track carries an empty content key,
        // and a unique index would need a provider-specific filter to let them coexist. The seeder is
        // what keeps one row per (key, language) — this index is here to make the lookup cheap.
        b.HasIndex(x => new { x.ContentKey, x.Language });
    }
}

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.ContentRef).HasMaxLength(512).IsRequired();
        b.HasIndex(x => new { x.TrackId, x.OrderNo });
        b.HasOne<LearningTrack>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TrackEnrollmentConfiguration : IEntityTypeConfiguration<TrackEnrollment>
{
    public void Configure(EntityTypeBuilder<TrackEnrollment> b)
    {
        b.ToTable("TrackEnrollments", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.TrackId, x.UserId }).IsUnique();
    }
}

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> b)
    {
        b.ToTable("LessonProgress", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
    }
}

public sealed class TrackCompletionConfiguration : IEntityTypeConfiguration<TrackCompletion>
{
    public void Configure(EntityTypeBuilder<TrackCompletion> b)
    {
        b.ToTable("TrackCompletions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.UserId, x.CompletedUtc });
        b.HasIndex(x => new { x.TrackId, x.UserId }).IsUnique();
    }
}

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.ToTable("Quizzes", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Intro).HasMaxLength(1024).IsRequired();

        // At most one quiz per track: the learner-facing question is "is there a quiz", not "which one".
        b.HasIndex(x => x.TrackId).IsUnique();
        b.HasOne<LearningTrack>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.ToTable("QuizQuestions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).HasMaxLength(1024).IsRequired();
        b.Property(x => x.Explanation).HasMaxLength(1024).IsRequired();
        b.HasIndex(x => new { x.QuizId, x.OrderNo });
        b.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> b)
    {
        b.ToTable("QuizAnswers", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).HasMaxLength(512).IsRequired();
        b.HasIndex(x => new { x.QuestionId, x.OrderNo });
        b.HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.ToTable("QuizAttempts", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(128).IsRequired();

        // Every read is "this person's attempts at this quiz, newest first" — for the best score, the
        // attempt number, and the review screen.
        b.HasIndex(x => new { x.QuizId, x.UserId, x.AttemptNo });
        b.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizAttemptAnswerConfiguration : IEntityTypeConfiguration<QuizAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAttemptAnswer> b)
    {
        b.ToTable("QuizAttemptAnswers", "koc");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.AttemptId);
        b.HasOne<QuizAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
    }
}
