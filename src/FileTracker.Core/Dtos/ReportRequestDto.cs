namespace FileTracker.Core.Dtos;

public class ReportRequestDto
{
    public int Month { get; set; }  // 1-12
    public int Year { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
}
