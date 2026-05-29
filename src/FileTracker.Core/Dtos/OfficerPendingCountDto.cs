namespace FileTracker.Core.Dtos;

/// <summary>
/// DTO for dashboard: officer name → pending document count.
/// </summary>
public class OfficerPendingCountDto
{
    public string OfficerName { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
}
