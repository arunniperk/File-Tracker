using FileTracker.Core.Dtos;

namespace FileTracker.Core.Services;

public interface IReportService
{
    Task<ReportDataDto> GenerateReportDataAsync(ReportRequestDto request);
    Task GeneratePdfReportAsync(ReportRequestDto request, string outputPath);
    Task GenerateExcelExportAsync(ReportRequestDto request, string outputPath);
}
