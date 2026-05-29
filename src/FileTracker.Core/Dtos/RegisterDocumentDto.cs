using FileTracker.Core.Models;

namespace FileTracker.Core.Dtos;

public class RegisterDocumentDto
{
    public DocumentDirection Direction { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string OriginalFileNumber { get; set; } = string.Empty;
    public string? Remarks { get; set; }

    public Document ToEntity(string? trackingId)
    {
        var now = DateTime.UtcNow;
        return new Document
        {
            Direction = Direction,
            Sender = Sender,
            Recipient = Recipient,
            Subject = Subject,
            DocumentDate = DocumentDate,
            OriginalFileNumber = OriginalFileNumber,
            TrackingId = trackingId,
            Remarks = Remarks,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };
    }
}
