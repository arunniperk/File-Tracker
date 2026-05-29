namespace FileTracker.Core.Models;

public class Movement
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int? FromPositionId { get; set; }
    public int ToPositionId { get; set; }
    public MovementDirection Direction { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation helpers — populated by JOIN queries, not stored in DB
    public string? FromPositionName { get; set; }
    public string? ToPositionName { get; set; }
}
