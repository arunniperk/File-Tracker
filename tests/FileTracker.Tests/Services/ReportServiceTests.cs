using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FileTracker.App.Services;
using FileTracker.Core.Dtos;
using FileTracker.Core.Models;
using FileTracker.Data;

namespace FileTracker.Tests.Services;

public class ReportServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DocumentRepository _docRepo = null!;
    private ReportService _reportService = null!;

    private const string CreateSchema = @"
        CREATE TABLE IF NOT EXISTS Documents (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Direction TEXT NOT NULL CHECK(Direction IN ('Incoming', 'Outgoing')),
            Sender TEXT,
            Recipient TEXT,
            Subject TEXT NOT NULL,
            DocumentDate TEXT NOT NULL,
            OriginalFileNumber TEXT NOT NULL,
            TrackingId TEXT,
            Remarks TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            IsDeleted INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS TrackingSequence (
            Year INTEGER PRIMARY KEY,
            LastNumber INTEGER NOT NULL DEFAULT 0
        );";

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync();

        await using var schemaCmd = _connection.CreateCommand();
        schemaCmd.CommandText = CreateSchema;
        await schemaCmd.ExecuteNonQueryAsync();

        var loggerFactory = new NullLoggerFactory();
        _docRepo = new DocumentRepository(_connection, loggerFactory.CreateLogger<DocumentRepository>());
        _reportService = new ReportService(_docRepo, loggerFactory.CreateLogger<ReportService>());
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Document> SeedDocument(string fileNumber, DocumentDirection direction,
        string? sender, string? recipient, DateTime documentDate)
    {
        var doc = new Document
        {
            Direction = direction,
            Sender = sender,
            Recipient = recipient,
            Subject = "Test Subject",
            DocumentDate = documentDate,
            OriginalFileNumber = fileNumber,
            TrackingId = "0001/2026",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        doc.Id = await _docRepo.InsertAsync(doc);
        return doc;
    }

    // ── Test 1: GenerateReportDataAsync returns correct total counts for mixed documents ──

    [Fact]
    public async Task GenerateReportDataAsync_ReturnsCorrectTotalCountsForMixedDocuments()
    {
        // Arrange: 2 incoming + 1 outgoing in May 2026
        await SeedDocument("RPT-001", DocumentDirection.Incoming, "Dept A", null,
            new DateTime(2026, 5, 1));
        await SeedDocument("RPT-002", DocumentDirection.Incoming, "Dept B", null,
            new DateTime(2026, 5, 10));
        await SeedDocument("RPT-003", DocumentDirection.Outgoing, null, "Dept C",
            new DateTime(2026, 5, 15));

        var request = new ReportRequestDto { Month = 5, Year = 2026 };

        // Act
        var result = await _reportService.GenerateReportDataAsync(request);

        // Assert
        result.TotalIncoming.Should().Be(2);
        result.TotalOutgoing.Should().Be(1);
        result.GrandTotal.Should().Be(3);
    }

    // ── Test 2: GenerateReportDataAsync returns zero totals for empty month ──

    [Fact]
    public async Task GenerateReportDataAsync_ReturnsZeroTotalsForEmptyMonth()
    {
        // Arrange: docs in May, but querying for June
        await SeedDocument("RPT-001", DocumentDirection.Incoming, "Dept A", null,
            new DateTime(2026, 5, 1));

        var request = new ReportRequestDto { Month = 6, Year = 2026 };

        // Act
        var result = await _reportService.GenerateReportDataAsync(request);

        // Assert
        result.TotalIncoming.Should().Be(0);
        result.TotalOutgoing.Should().Be(0);
        result.GrandTotal.Should().Be(0);
        result.Documents.Should().BeEmpty();
    }

    // ── Test 3: GenerateReportDataAsync computes ByDirection breakdown correctly ──

    [Fact]
    public async Task GenerateReportDataAsync_ComputesByDirectionBreakdown()
    {
        // Arrange
        await SeedDocument("RPT-IN-1", DocumentDirection.Incoming, "Dept A", null,
            new DateTime(2026, 5, 1));
        await SeedDocument("RPT-IN-2", DocumentDirection.Incoming, "Dept B", null,
            new DateTime(2026, 5, 5));
        await SeedDocument("RPT-OUT-1", DocumentDirection.Outgoing, null, "Dept C",
            new DateTime(2026, 5, 10));

        var request = new ReportRequestDto { Month = 5, Year = 2026 };

        // Act
        var result = await _reportService.GenerateReportDataAsync(request);

        // Assert
        result.ByDirection.Should().Contain(kv => kv.Key == "Incoming" && kv.Value == 2);
        result.ByDirection.Should().Contain(kv => kv.Key == "Outgoing" && kv.Value == 1);
        result.ByDirection.Should().HaveCount(2);
    }

    // ── Test 4: GenerateReportDataAsync computes BySenderRecipient breakdown ──

    [Fact]
    public async Task GenerateReportDataAsync_ComputesBySenderRecipientBreakdown()
    {
        // Arrange: incoming docs use Sender, outgoing docs use Recipient
        await SeedDocument("RPT-A1", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 1));
        await SeedDocument("RPT-A2", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 5));
        await SeedDocument("RPT-B1", DocumentDirection.Incoming, "Dean Office", null,
            new DateTime(2026, 5, 10));
        await SeedDocument("RPT-C1", DocumentDirection.Outgoing, null, "Finance Dept",
            new DateTime(2026, 5, 15));

        var request = new ReportRequestDto { Month = 5, Year = 2026 };

        // Act
        var result = await _reportService.GenerateReportDataAsync(request);

        // Assert
        result.BySenderRecipient.Should().Contain(kv => kv.Key == "Registrar" && kv.Value == 2);
        result.BySenderRecipient.Should().Contain(kv => kv.Key == "Dean Office" && kv.Value == 1);
        result.BySenderRecipient.Should().Contain(kv => kv.Key == "Finance Dept" && kv.Value == 1);
    }

    // ── Test 5: GeneratePdfReportAsync creates a non-empty PDF file ──

    [Fact]
    public async Task GeneratePdfReportAsync_CreatesNonEmptyPdfFile()
    {
        // Arrange
        await SeedDocument("RPT-PDF", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 1));
        var request = new ReportRequestDto { Month = 5, Year = 2026 };
        var outputPath = Path.GetTempFileName() + ".pdf";

        try
        {
            // Act
            await _reportService.GeneratePdfReportAsync(request, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    // ── Test 6: GeneratePdfReportAsync output is valid PDF ──

    [Fact]
    public async Task GeneratePdfReportAsync_OutputIsValidPdf()
    {
        // Arrange
        await SeedDocument("RPT-PDF-V", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 1));
        var request = new ReportRequestDto { Month = 5, Year = 2026 };
        var outputPath = Path.GetTempFileName() + ".pdf";

        try
        {
            // Act
            await _reportService.GeneratePdfReportAsync(request, outputPath);

            // Assert: valid PDF starts with "%PDF" magic bytes
            var bytes = await File.ReadAllBytesAsync(outputPath);
            bytes.Length.Should().BeGreaterThan(4);
            bytes[0].Should().Be(0x25); // '%'
            bytes[1].Should().Be(0x50); // 'P'
            bytes[2].Should().Be(0x44); // 'D'
            bytes[3].Should().Be(0x46); // 'F'
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    // ── Test 7: GenerateExcelExportAsync creates a non-empty .xlsx file ──

    [Fact]
    public async Task GenerateExcelExportAsync_CreatesNonEmptyXlsxFile()
    {
        // Arrange
        await SeedDocument("RPT-XLSX", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 1));
        var request = new ReportRequestDto { Month = 5, Year = 2026 };
        var outputPath = Path.GetTempFileName() + ".xlsx";

        try
        {
            // Act
            await _reportService.GenerateExcelExportAsync(request, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    // ── Test 8: GenerateExcelExportAsync can be reopened with ClosedXML ──

    [Fact]
    public async Task GenerateExcelExportAsync_CanBeReopenedWithClosedXml()
    {
        // Arrange
        await SeedDocument("RPT-XLSX-R", DocumentDirection.Incoming, "Registrar", null,
            new DateTime(2026, 5, 1));
        var request = new ReportRequestDto { Month = 5, Year = 2026 };
        var outputPath = Path.GetTempFileName() + ".xlsx";

        try
        {
            // Act
            await _reportService.GenerateExcelExportAsync(request, outputPath);

            // Assert: reopen with ClosedXML without errors
            using var workbook = new ClosedXML.Excel.XLWorkbook(outputPath);
            workbook.Worksheets.Count.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
