namespace SoccerWizard.Api;

/// <summary>
/// DTO untuk response API — memudahkan Swagger documentation.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public int TotalCount { get; set; }

    public static ApiResponse<T> Ok(T data, int totalCount = 0) => new()
    {
        Success = true,
        Data = data,
        TotalCount = totalCount > 0 ? totalCount : (data is System.Collections.ICollection col ? col.Count : 1)
    };

    public static ApiResponse<T> Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPrevPage => Page > 1;
}

public class MatchFilterRequest
{
    public string? Status { get; set; }
    public int? LeagueId { get; set; }
    public int? TeamId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
