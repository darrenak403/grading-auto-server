namespace PRN232.LMS.Services.Models;

/// <summary>
/// Query parameters for list APIs: search, sort, paging, field selection, expansion
/// </summary>
public class QueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    /// <summary>Filter by keyword</summary>
    public string? Search { get; set; }

    /// <summary>Sort fields: "fieldName" ASC, "-fieldName" DESC. Multiple: "fullName,-dateOfBirth"</summary>
    public string? Sort { get; set; }

    /// <summary>Page number (1-based)</summary>
    public int Page { get; set; } = 1;

    /// <summary>Page size (max 100)</summary>
    public int Size
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 10 : value;
    }

    /// <summary>Comma-separated field names to return</summary>
    public string? Fields { get; set; }

    /// <summary>Comma-separated related entities to include</summary>
    public string? Expand { get; set; }
}
