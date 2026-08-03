using Beep.KocAiCommunity.Contracts.Learning;

namespace Beep.KocAiCommunity.Application.Learning;

/// <summary>Raised when a quiz cannot be sat or saved, with a message meant for the person who asked.</summary>
public sealed class QuizException(string message) : Exception(message);

/// <summary>
/// End-of-track quizzes.
/// <para>
/// The learner methods and the admin methods return different shapes on purpose: only the admin ones
/// carry which answer is correct. The split is in the types rather than in a flag, so disclosing the
/// answers from a learner endpoint is a compile error and not an oversight.
/// </para>
/// </summary>
public interface IQuizService
{
    /// <summary>
    /// The quiz for a track as a learner sees it — no correct answers — with their own history folded
    /// in. Null when the track has no quiz, or has one that is switched off.
    /// </summary>
    Task<QuizDto?> GetForLearnerAsync(Guid trackId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Grades a sitting, records it, and re-checks whether the track is now finished. Throws when the
    /// quiz is missing, switched off, or has nothing to answer.
    /// </summary>
    Task<QuizAttemptResultDto> SubmitAsync(
        Guid trackId, string userId, SubmitQuizRequest request, CancellationToken ct = default);

    /// <summary>This person's past sittings of a track's quiz, newest first.</summary>
    Task<IReadOnlyList<QuizAttemptSummaryDto>> GetMyAttemptsAsync(
        Guid trackId, string userId, CancellationToken ct = default);

    // ---- Admin ----

    /// <summary>The quiz with its correct answers. Null when the track has none yet.</summary>
    Task<AdminQuizDto?> GetForAdminAsync(Guid trackId, CancellationToken ct = default);

    /// <summary>Creates the track's quiz on first save and updates it after.</summary>
    Task<AdminQuizDto> UpsertAsync(Guid trackId, UpsertQuizRequest request, string adminUserId, CancellationToken ct = default);

    /// <summary>Adds or replaces one question and its answers, saved as a unit.</summary>
    Task<AdminQuizDto> SaveQuestionAsync(
        Guid trackId, UpsertQuizQuestionRequest request, string adminUserId, CancellationToken ct = default);

    Task<AdminQuizDto> DeleteQuestionAsync(Guid trackId, Guid questionId, string adminUserId, CancellationToken ct = default);
}
