using FileTracker.Core.Dtos;
using FileTracker.Core.Services;
using FileTracker.Data;
using Microsoft.Extensions.Logging;

namespace FileTracker.App.Services;

public class ReportService : IReportService
{
    private readonly IDocumentRepository _docRepo;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IDocumentRepository docRepo, ILogger<ReportService> logger)
    {
        _docRepo = docRepo;
        _logger = logger;
    }

    public Task<ReportDataDto> GenerateReportDataAsync(ReportRequestDto request)
    {
        throw new NotImplementedException();
    }

    public Task GeneratePdfReportAsync(ReportRequestDto request, string outputPath)
    {
        throw new NotImplementedException();
    }

    public Task GenerateExcelExportAsync(ReportRequestDto request, string outputPath)
    {
        throw new NotImplementedException();
    }
}
