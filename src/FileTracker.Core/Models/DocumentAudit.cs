namespace FileTracker.Core.Models;

public class DocumentAudit
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
}
