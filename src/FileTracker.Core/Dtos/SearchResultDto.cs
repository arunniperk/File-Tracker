using FileTracker.Core.Models;

namespace FileTracker.Core.Dtos;

public class SearchResultDto
{
    public IReadOnlyList<Document> Results { get; set; } = Array.Empty<Document>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
