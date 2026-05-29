namespace FileTracker.Core.Models;

public class Document
{
    public int Id { get; set; }
    public DocumentDirection Direction { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string OriginalFileNumber { get; set; } = string.Empty;
    public string? TrackingId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Display-only: current location derived from most recent movement.
    /// NOT stored in the Documents table — populated by the application layer after querying movements.
    /// </summary>
    public string CurrentLocation { get; set; } = "\u2014";
}
