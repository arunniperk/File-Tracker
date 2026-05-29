using ClosedXML.Excel;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Core.Services;
using FileTracker.Data;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPdfDocument = QuestPDF.Fluent.Document;

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

    public async Task<ReportDataDto> GenerateReportDataAsync(ReportRequestDto request)
    {
        var documents = await _docRepo.GetByMonthAsync(request.Year, request.Month);

        var data = new ReportDataDto
        {
            Request = request,
            Documents = documents,
            TotalIncoming = documents.Count(d => d.Direction == DocumentDirection.Incoming),
            TotalOutgoing = documents.Count(d => d.Direction == DocumentDirection.Outgoing)
        };

        // ByDirection breakdown
        data.ByDirection = documents
            .GroupBy(d => d.Direction.ToString())
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .OrderByDescending(kv => kv.Value)
            .ToList();

        // BySenderRecipient breakdown: Sender for incoming, Recipient for outgoing
        data.BySenderRecipient = documents
            .Select(d => d.Direction == DocumentDirection.Incoming ? d.Sender : d.Recipient)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key!)
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .ToList();

        _logger.LogInformation(
            "Report generated for {Month}/{Year}: {Incoming} incoming, {Outgoing} outgoing, {Total} total documents",
            request.Month, request.Year, data.TotalIncoming, data.TotalOutgoing, data.GrandTotal);

        return data;
    }

    public async Task GeneratePdfReportAsync(ReportRequestDto request, string outputPath)
    {
        var data = await GenerateReportDataAsync(request);

        QuestPdfDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Column(header =>
                {
                    header.Item().Text("IIT Dharwad — Registrar Office")
                        .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);

                    header.Item().Text($"Monthly Report — {data.Request.MonthName}")
                        .FontSize(12).Bold();

                    header.Item().Text($"Generated: {DateTime.Now:dd MMMM yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);

                    header.Item().PaddingVertical(4);

                    // Summary table
                    header.Item().Table(summary =>
                    {
                        summary.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        summary.Header(headerRow =>
                        {
                            headerRow.Cell().Background(Colors.Grey.Lighten2)
                                .Padding(3).Text("Incoming").Bold().FontSize(10);
                            headerRow.Cell().Background(Colors.Grey.Lighten2)
                                .Padding(3).Text("Outgoing").Bold().FontSize(10);
                            headerRow.Cell().Background(Colors.Grey.Lighten2)
                                .Padding(3).Text("Grand Total").Bold().FontSize(10);
                        });

                        summary.Cell().Padding(3).AlignCenter()
                            .Text(data.TotalIncoming.ToString()).FontSize(12).Bold();
                        summary.Cell().Padding(3).AlignCenter()
                            .Text(data.TotalOutgoing.ToString()).FontSize(12).Bold();
                        summary.Cell().Padding(3).AlignCenter()
                            .Text(data.GrandTotal.ToString()).FontSize(12).Bold()
                            .FontColor(Colors.Blue.Darken1);
                    });
                });

                // Content
                page.Content().Column(content =>
                {
                    // Section 1: By Direction breakdown
                    content.Item().PaddingTop(8).Text("Breakdown by Direction")
                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                    if (data.ByDirection.Count > 0)
                    {
                        content.Item().Table(dirTable =>
                        {
                            dirTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                            });

                            dirTable.Header(hr =>
                            {
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Direction").Bold().FontSize(9);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Count").Bold().FontSize(9);
                            });

                            foreach (var kv in data.ByDirection)
                            {
                                dirTable.Cell().Padding(2).Text(kv.Key).FontSize(9);
                                dirTable.Cell().Padding(2).AlignCenter()
                                    .Text(kv.Value.ToString()).FontSize(9);
                            }
                        });
                    }

                    content.Item().PaddingTop(4).Text(data.TypeNote)
                        .FontSize(7).Italic().FontColor(Colors.Grey.Medium);

                    // Section 2: By Department (Sender/Recipient)
                    content.Item().PaddingTop(10).Text("Breakdown by Department (Sender / Recipient)")
                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                    if (data.BySenderRecipient.Count > 0)
                    {
                        content.Item().Table(deptTable =>
                        {
                            deptTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1);
                            });

                            deptTable.Header(hr =>
                            {
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Department / Entity").Bold().FontSize(9);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Count").Bold().FontSize(9);
                            });

                            foreach (var kv in data.BySenderRecipient)
                            {
                                deptTable.Cell().Padding(2).Text(kv.Key).FontSize(9);
                                deptTable.Cell().Padding(2).AlignCenter()
                                    .Text(kv.Value.ToString()).FontSize(9);
                            }
                        });
                    }

                    content.Item().PaddingTop(4).Text(data.PriorityNote)
                        .FontSize(7).Italic().FontColor(Colors.Grey.Medium);

                    // Section 3: Document List
                    content.Item().PaddingTop(10).Text("Document List")
                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                    if (data.Documents.Count > 0)
                    {
                        content.Item().Table(docTable =>
                        {
                            docTable.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(60);  // Tracking ID
                                cols.ConstantColumn(50);  // Direction
                                cols.RelativeColumn(2);   // Sender/Recipient
                                cols.RelativeColumn(3);   // Subject
                                cols.ConstantColumn(65);  // Date
                                cols.RelativeColumn(2);   // File #
                            });

                            docTable.Header(hr =>
                            {
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Tracking ID").Bold().FontSize(8);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Dir").Bold().FontSize(8);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Sender/Recipient").Bold().FontSize(8);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Subject").Bold().FontSize(8);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Date").Bold().FontSize(8);
                                hr.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(2).Text("Orig File #").Bold().FontSize(8);
                            });

                            foreach (var doc in data.Documents)
                            {
                                var senderOrRecipient = doc.Direction == DocumentDirection.Incoming
                                    ? (doc.Sender ?? "")
                                    : (doc.Recipient ?? "");

                                docTable.Cell().Padding(2).Text(doc.TrackingId ?? "").FontSize(8);
                                docTable.Cell().Padding(2).Text(doc.Direction.ToString()).FontSize(8);
                                docTable.Cell().Padding(2).Text(senderOrRecipient).FontSize(8);
                                docTable.Cell().Padding(2).Text(doc.Subject).FontSize(8);
                                docTable.Cell().Padding(2).Text(doc.DocumentDate.ToString("dd-MMM-yyyy")).FontSize(8);
                                docTable.Cell().Padding(2).Text(doc.OriginalFileNumber).FontSize(8);
                            }
                        });
                    }
                    else
                    {
                        content.Item().PaddingTop(4).Text("No documents found for the selected period.")
                            .FontSize(10).Italic();
                    }
                });

                // Footer with page numbers
                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(ts => ts.FontSize(8).FontColor(Colors.Grey.Medium));
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf(outputPath);

        _logger.LogInformation("PDF report saved to {Path}", outputPath);
    }

    public async Task GenerateExcelExportAsync(ReportRequestDto request, string outputPath)
    {
        var data = await GenerateReportDataAsync(request);

        using var workbook = new XLWorkbook();
        var sheetName = $"Documents_{request.Month:D2}_{request.Year}";
        var worksheet = workbook.AddWorksheet(sheetName);

        // Header row
        var headers = new[] { "Tracking ID", "Direction", "Sender", "Recipient",
            "Subject", "Document Date", "Original File Number", "Remarks", "Created At" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRow = worksheet.Range(1, 1, 1, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data rows
        int row = 2;
        foreach (var doc in data.Documents)
        {
            worksheet.Cell(row, 1).Value = doc.TrackingId ?? "";
            worksheet.Cell(row, 2).Value = doc.Direction.ToString();
            worksheet.Cell(row, 3).Value = doc.Sender ?? "";
            worksheet.Cell(row, 4).Value = doc.Recipient ?? "";
            worksheet.Cell(row, 5).Value = doc.Subject;
            worksheet.Cell(row, 6).Value = doc.DocumentDate.ToString("dd-MMM-yyyy");
            worksheet.Cell(row, 7).Value = doc.OriginalFileNumber;
            worksheet.Cell(row, 8).Value = doc.Remarks ?? "";
            worksheet.Cell(row, 9).Value = doc.CreatedAt.ToString("dd-MMM-yyyy HH:mm");
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(outputPath);

        _logger.LogInformation("Excel report saved to {Path} with {RowCount} rows", outputPath, data.Documents.Count);
    }
}
