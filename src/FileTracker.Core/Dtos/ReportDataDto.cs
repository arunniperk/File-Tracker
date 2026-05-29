using FileTracker.Core.Models;

namespace FileTracker.Core.Dtos;

public class ReportDataDto
{
    public ReportRequestDto Request { get; set; } = new();
    public int TotalIncoming { get; set; }
    public int TotalOutgoing { get; set; }
    public int GrandTotal => TotalIncoming + TotalOutgoing;

    /// <summary>Breakdown: Direction (Incoming / Outgoing) with counts.</summary>
    public List<KeyValuePair<string, int>> ByDirection { get; set; } = new();

    /// <summary>
    /// Breakdown: grouped by Sender (for incoming) or Recipient (for outgoing).
    /// Top 10 entries by count descending.
    /// </summary>
    public List<KeyValuePair<string, int>> BySenderRecipient { get; set; } = new();

    /// <summary>Note explaining priority tracking availability.</summary>
    public string PriorityNote => "Priority tracking is not available in the current version.";

    /// <summary>Note explaining type breakdown is shown by Direction.</summary>
    public string TypeNote => "Document type breakdown is shown by Direction (Incoming vs Outgoing).";

    /// <summary>All documents for the report period.</summary>
    public IReadOnlyList<Document> Documents { get; set; } = Array.Empty<Document>();
}
