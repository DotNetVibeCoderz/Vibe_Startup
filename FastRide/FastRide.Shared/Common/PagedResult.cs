namespace FastRide.Shared.Common;

/// <summary>
/// Envelope returned by every list endpoint. Clients bind to this directly.
/// </summary>
public class PagedResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 25;
    public List<T> Data { get; set; } = new();

    public int TotalPages => Limit <= 0 ? 0 : (int)Math.Ceiling((double)Total / Limit);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page = 1, int limit = 25) => new() { Page = page, Limit = limit };
}

/// <summary>Normalises paging input so a client cannot ask for 1,000,000 rows.</summary>
public readonly record struct PageRequest(int Page, int Limit)
{
    public const int MaxLimit = 200;

    public static PageRequest From(int? page, int? limit, int defaultLimit = 25) =>
        new(Math.Max(1, page ?? 1), Math.Clamp(limit ?? defaultLimit, 1, MaxLimit));

    public int Skip => (Page - 1) * Limit;
}

/// <summary>Uniform error body so every client can show the same message.</summary>
public record ApiError(string Error, string? Detail = null);
