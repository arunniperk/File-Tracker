namespace FileTracker.Core.Dtos;

public class SearchDocumentDto
{
    public string? OriginalFileNumber { get; set; }
    public string? TrackingId { get; set; }
    public string? Subject { get; set; }
    public string? SenderOrRecipient { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
