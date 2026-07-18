namespace Beep.KocAiCommunity.Contracts.Common;

/// <summary>Standard paged envelope returned by list endpoints.</summary>
/// <typeparam name="T">Item DTO type.</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
