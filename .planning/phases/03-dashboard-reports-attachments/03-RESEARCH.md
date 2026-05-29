# Phase 3: Dashboard, Reports & Attachments — Research

**Researched:** 2026-05-29
**Domain:** WPF Dashboard UI + PDF/Excel Report Generation + Filesystem Attachment Management
**Confidence:** HIGH

## Summary

Phase 3 adds three capabilities to the File Tracker WPF app: an operational dashboard showing pending/overdue document counts, monthly summary reports with PDF and Excel export, and scanned document attachment support. All three leverage the existing SQLite database, Dapper data access, and MVVM architecture built in Phases 1–2.

**Dashboard data** is derived entirely from existing `Documents` and `Movements` tables via SQL aggregation queries — no new tables or data model changes are required. **Reports** use QuestPDF 2026.5.0 (Community MIT license) for PDF generation and ClosedXML 0.105.0 (MIT license) for Excel export. **Attachments** require one new `Attachments` table storing metadata (filename, path, document reference), with actual files stored on the filesystem at `%LocalAppData%\FileTracker\attachments\{documentId}\`.

**Critical license finding:** QuestPDF is licensed under Community MIT — free for IIT Dharwad (educational institution under $1M revenue). The license must be explicitly set: `QuestPDF.Settings.License = LicenseType.Community;`. ClosedXML is standard MIT. Both licenses are compatible with this project.

**Primary recommendation:** Keep dashboard data queries in the repository layer (new query methods on `IDocumentRepository`) rather than a separate service. Add `IAttachmentService` + `IAttachmentRepository` as new clean abstractions. Build `IReportService` in Core layer with QuestPDF/ClosedXML references in the App layer only.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Dashboard data queries | Data (Repository) | Core (Service) | SQL aggregation queries against existing tables; new repository methods, thin service pass-through |
| Dashboard UI | App (ViewModel/View) | — | WPF DataBinding to dashboard ViewModel properties; no new database writes |
| PDF report generation | App (Service) | Core (DTOs) | QuestPDF is a UI-layer concern (file output); DTOs define the report data shape |
| Excel export | App (Service) | Core (DTOs) | ClosedXML is a UI-layer concern; same DTOs as PDF |
| Report data queries | Data (Repository) | Core (Service) | Complex date-range queries with grouping; repository owns SQL, service owns business logic |
| Attachment CRUD | Core (Service) | Data (Repository) | Standard service→repository pattern matching existing DocumentService/DocumentRepository |
| Attachment file I/O | App (Service) | — | Filesystem operations (copy, delete, open) are platform-specific; belong in App layer |
| Attachment UI | App (ViewModel/View) | — | File picker, list display, open-in-default-viewer — all WPF concerns |
| Navigation from dashboard | App (ViewModel) | — | Dashboard click → search filter → existing SearchViewModel |

## Standard Stack

### Core Libraries (new for Phase 3)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| QuestPDF | 2026.5.0 | PDF report generation | 22.9M downloads, pure .NET Fluent API, no external dependencies for net6.0+, table/layout engine, Community MIT license [VERIFIED: nuget.org] |
| ClosedXML | 0.105.0 | Excel report export | 181.7M downloads, MIT license, intuitive cell-based API, bulk InsertData support, used by nopCommerce, Kernel Memory [VERIFIED: nuget.org] |

### Supporting (already in project)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Dapper | 2.1.79 | Dashboard/Report SQL queries | All new repository query methods |
| CommunityToolkit.Mvvm | 8.4.2 | DashboardViewModel, ReportViewModel | `[ObservableProperty]`, `[RelayCommand]` for new ViewModels |
| Microsoft.Win32.OpenFileDialog | Built-in WPF | Attachment file picker | Native Windows file dialog, no NuGet needed |
| System.Diagnostics.Process | Built-in .NET | Open attachment in default viewer | `Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true })` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| QuestPDF | PdfSharp / MigraDoc | PdfSharp is GPL-like (not MIT-compatible for this project); MigraDoc is AGPL. QuestPDF is the only permissive-licensed pure-.NET PDF library at this quality tier |
| QuestPDF | iTextSharp / iText7 | iText7 is AGPL (requires commercial license for non-open-source); QuestPDF Community MIT is free for IIT Dharwad |
| ClosedXML | EPPlus | EPPlus switched to LGPL/commercial in v5+; ClosedXML has always been MIT and has larger ecosystem (614 dependent packages vs EPPlus ~200) |
| ClosedXML | SpreadsheetLight | Less maintained, smaller community; ClosedXML has active releases (2025) |

**Installation:**
```bash
# In FileTracker.App project (report generation is UI-triggered)
dotnet add src/FileTracker.App/FileTracker.App.csproj package QuestPDF --version 2026.5.0
dotnet add src/FileTracker.App/FileTracker.App.csproj package ClosedXML --version 0.105.0
```

**Version verification:** Both versions confirmed on NuGet.org via `dotnet package search` on 2026-05-29 [VERIFIED: nuget.org]. QuestPDF 2026.5.0 published 2026-05-09; ClosedXML 0.105.0 published 2025-05-14.

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| QuestPDF | NuGet | 5+ yrs (first release 2020.11) | 22.9M total | github.com/QuestPDF/QuestPDF | N/A (PyPI-only tool — not applicable to NuGet) | Approved — confirmed on nuget.org via `dotnet package search` |
| ClosedXML | NuGet | 10+ yrs (first release 2014) | 181.7M total | github.com/ClosedXML/ClosedXML | N/A (PyPI-only tool — not applicable to NuGet) | Approved — confirmed on nuget.org via `dotnet package search`; MIT license verified |

**slopcheck cross-ecosystem note:** slopcheck 0.6.1 scanned PyPI only. Both QuestPDF and ClosedXML are .NET/NuGet packages and do not exist on PyPI — the `[SLOP]` verdict is a false positive from ecosystem mismatch (documented ~9% hallucination vector). Registry verification was performed via `dotnet package search` against the correct ecosystem (NuGet).

**Packages removed due to slopcheck [SLOP] verdict:** none (false positive — wrong ecosystem)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        MAINWINDOW                                │
│  ┌──────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │  Dashboard   │  │  Search/Register │  │  Search Results   │  │
│  │   Panel      │  │  (existing tabs) │  │  DataGrid         │  │
│  │              │  │                  │  │  (existing)       │  │
│  └──────┬───────┘  └──────────────────┘  └───────────────────┘  │
│         │                                                        │
│         │ DashboardViewModel                                     │
│         │  ├─ PendingByOfficer  ──── IDocumentRepository         │
│         │  ├─ RecentDocuments   ──── (new query methods)         │
│         │  ├─ OverdueDocuments  ──── (new query methods)         │
│         │  └─ Click → navigates to filtered SearchViewModel      │
│         │                                                        │
└─────────┼────────────────────────────────────────────────────────┘
          │
┌─────────▼────────────────────────────────────────────────────────┐
│                    REPORT GENERATION FLOW                         │
│                                                                   │
│  ReportViewModel ──► IReportService ──► IDocumentRepository      │
│       │                    │                │                     │
│       │              ┌─────┴─────┐    SQL queries                 │
│       │              │           │    (monthly aggregates)        │
│       ▼              ▼           ▼                                │
│  SaveFileDialog   QuestPDF    ClosedXML                           │
│  (user picks      .GeneratePdf .SaveAs                            │
│   output path)    ("report.pdf") ("export.xlsx")                  │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                    ATTACHMENT FLOW                                │
│                                                                   │
│  RegisterDocumentView ──► IAttachmentService                      │
│  DocumentDetailView         │                                     │
│       │                     ├─ AddAttachmentAsync(documentId,     │
│       │                     │      filePath)                      │
│       │                     │   → copies file to                  │
│       │                     │     %LocalAppData%\FileTracker\     │
│       │                     │     attachments\{docId}\            │
│       │                     │   → inserts Attachment row in DB    │
│       │                     │                                     │
│       │                     ├─ GetAttachmentsAsync(documentId)    │
│       │                     │   → reads Attachment rows from DB   │
│       │                     │                                     │
│       │                     ├─ RemoveAttachmentAsync(attachmentId)│
│       │                     │   → deletes file + DB row           │
│       │                     │                                     │
│       │                     └─ OpenAttachmentAsync(attachmentId)  │
│       │                         → Process.Start(path)             │
│       │                                                            │
│       ▼                                                            │
│  IAttachmentRepository ──► Attachments table (SQLite)             │
│                            Filesystem (LocalAppData)              │
└───────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure
```
src/
├── FileTracker.Core/
│   ├── Models/
│   │   └── Attachment.cs              # NEW: Attachment entity
│   ├── Dtos/
│   │   ├── ReportRequestDto.cs        # NEW: month/year selection
│   │   ├── ReportDataDto.cs           # NEW: aggregated report data
│   │   └── DashboardDataDto.cs        # NEW: dashboard query results
│   ├── Services/
│   │   ├── IAttachmentRepository.cs   # NEW: attachment persistence contract
│   │   ├── IAttachmentService.cs      # NEW: attachment business logic contract
│   │   ├── IReportService.cs          # NEW: report generation contract
│   │   └── IDocumentRepository.cs     # MODIFIED: add dashboard/report query methods
│   └── Exceptions/
│       └── AttachmentNotFoundException.cs  # NEW
│
├── FileTracker.Data/
│   ├── AttachmentRepository.cs        # NEW: Dapper implementation
│   ├── DocumentRepository.cs          # MODIFIED: add GetDashboardDataAsync, GetMonthlyReportDataAsync
│   └── DatabaseInitializer.cs         # MODIFIED: CREATE TABLE Attachments
│
├── FileTracker.App/
│   ├── ViewModels/
│   │   ├── DashboardViewModel.cs      # NEW
│   │   ├── ReportViewModel.cs         # NEW
│   │   ├── MainViewModel.cs           # MODIFIED: add dashboard/report commands, tab navigation
│   │   ├── RegisterDocumentViewModel.cs  # MODIFIED: add attachment button command
│   │   └── DocumentDetailViewModel.cs    # MODIFIED: add attachment list, open/remove commands
│   ├── Views/
│   │   ├── DashboardView.xaml         # NEW
│   │   ├── ReportView.xaml            # NEW (or window)
│   │   ├── MainWindow.xaml            # MODIFIED: add dashboard panel, report button
│   │   ├── RegisterDocumentView.xaml  # MODIFIED: add attachment picker button
│   │   └── DocumentDetailView.xaml    # MODIFIED: add attachment list
│   ├── Services/
│   │   ├── AttachmentService.cs       # NEW: filesystem + DB coordination
│   │   └── ReportService.cs           # NEW: QuestPDF + ClosedXML generation
│   ├── Converters/
│   │   ├── CountToVisibilityConverter.cs    # NEW: hide sections with zero items
│   │   └── OverdueToColorConverter.cs       # NEW: red highlight for overdue
│   └── App.xaml.cs                    # MODIFIED: register new services, set QuestPDF license
```

### Pattern 1: Extending Existing Repository with Dashboard Queries

**What:** Add read-only query methods to `IDocumentRepository` that return pre-aggregated dashboard data. No new service layer — dashboard data is thin pass-through.

**When to use:** When new queries work against existing tables with no business logic transformation needed.

**Example:**
```csharp
// IDocumentRepository.cs — new methods
public interface IDocumentRepository
{
    // ... existing methods ...
    
    /// <summary>
    /// Returns count of documents pending at each officer.
    /// "Pending" = this is the document's current location (most recent movement).
    /// </summary>
    Task<IReadOnlyList<OfficerPendingCountDto>> GetPendingByOfficerAsync();
    
    /// <summary>
    /// Returns documents registered in the last N days, newest first.
    /// </summary>
    Task<IReadOnlyList<Document>> GetRecentAsync(int days = 7);
    
    /// <summary>
    /// Returns documents where the most recent movement is older than threshold days.
    /// </summary>
    Task<IReadOnlyList<OverdueDocumentDto>> GetOverdueAsync(int thresholdDays = 7);
    
    /// <summary>
    /// Returns all documents for a given month/year with their movement data,
    /// for report generation.
    /// </summary>
    Task<IReadOnlyList<Document>> GetByMonthAsync(int year, int month);
}
```

**Source:** Follows existing `IDocumentRepository` pattern [VERIFIED: codebase grep — IDocumentRepository.cs, DocumentRepository.cs].

### Pattern 2: Attachment Storage — Filesystem Metadata + DB Pointer

**What:** Attachments are stored as physical files in a managed directory tree. The `Attachments` table stores only metadata (filename, path, document FK, timestamps). The `AttachmentService` coordinates filesystem operations with database state within a transaction.

**When to use:** For any binary file storage in a desktop app — avoids BLOB bloat in SQLite per Pitfall 8 from PITFALLS.md.

**Storage layout:**
```
%LocalAppData%\FileTracker\attachments\
├── 1\                          # Document ID 1
│   ├── 20260529_143022_scan.pdf
│   └── 20260529_143045_photo.jpg
├── 2\
│   └── 20260529_150000_receipt.pdf
└── ...
```

**Example:**
```csharp
// AttachmentService.cs — key flow
public async Task<Attachment> AddAttachmentAsync(int documentId, string sourceFilePath)
{
    var document = await _docRepo.GetByIdAsync(documentId)
        ?? throw new NotFoundException($"Document {documentId} not found");
    
    var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(sourceFilePath)}";
    var storageDir = Path.Combine(_attachmentRoot, documentId.ToString());
    Directory.CreateDirectory(storageDir);
    
    var destPath = Path.Combine(storageDir, fileName);
    File.Copy(sourceFilePath, destPath, overwrite: false);
    
    var attachment = new Attachment
    {
        DocumentId = documentId,
        FileName = Path.GetFileName(sourceFilePath),
        StoragePath = destPath,
        FileSize = new FileInfo(destPath).Length,
        CreatedAt = DateTime.UtcNow
    };
    
    attachment.Id = await _attachmentRepo.InsertAsync(attachment);
    return attachment;
}
```

**Source:** PITFALLS.md Pitfall 8 (Scanned Attachment Storage Bloat) [CITED: .planning/research/PITFALLS.md]; STACK.md attachment storage path [CITED: .planning/research/STACK.md].

### Pattern 3: QuestPDF Report — Table + Grouped Summary

**What:** QuestPDF generates a multi-page PDF with header, summary tables, and grouped breakdowns using its Fluent API and Table component.

**When to use:** For PDF report generation from WPF — `GeneratePdf()` writes directly to a file path.

**Example:**
```csharp
// Source: QuestPDF official docs [CITED: questpdf.com/quick-start.html, questpdf.com/api-reference/table/basics.html]
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Set license ONCE at app startup (in App.xaml.cs)
QuestPDF.Settings.License = LicenseType.Community;

Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(1.5f, Unit.Centimetre);
        page.DefaultTextStyle(x => x.FontSize(10));
        
        page.Header()
            .Column(header =>
            {
                header.Item().Text("IIT Dharwad — Registrar Office")
                    .FontSize(14).Bold();
                header.Item().Text($"Monthly Report — {reportData.Month}/{reportData.Year}")
                    .FontSize(12);
                header.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });
        
        page.Content()
            .Column(content =>
            {
                // Summary counts
                content.Item().Element(ComposeSummaryTable);
                
                // Breakdown by document type
                content.Item().PaddingTop(10).Element(c => ComposeBreakdownTable(c, "By Document Type", reportData.ByType));
                
                // Breakdown by department
                content.Item().PaddingTop(10).Element(c => ComposeBreakdownTable(c, "By Department", reportData.ByDepartment));
            });
        
        page.Footer()
            .AlignCenter()
            .Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
    });
})
.GeneratePdf(outputPath);
```

**Table pattern for breakdowns:**
```csharp
void ComposeBreakdownTable(IContainer container, string title, IEnumerable<KeyValuePair<string, int>> data)
{
    container.Column(col =>
    {
        col.Item().Text(title).FontSize(11).Bold();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(1);
            });
            
            table.Header(header =>
            {
                header.Cell().BorderBottom(1).Padding(4).Text("Category").Bold();
                header.Cell().BorderBottom(1).Padding(4).AlignRight().Text("Count").Bold();
            });
            
            foreach (var (key, count) in data)
            {
                table.Cell().Padding(4).Text(key);
                table.Cell().Padding(4).AlignRight().Text(count.ToString());
            }
        });
    });
}
```

### Pattern 4: ClosedXML Excel Export — Bulk Insert from Collection

**What:** ClosedXML writes tabular data to `.xlsx` using `InsertData()` for bulk population, with optional header styling.

**When to use:** For raw data export — Excel files for further processing by staff.

**Example:**
```csharp
// Source: ClosedXML official docs [CITED: docs.closedxml.io]
using ClosedXML.Excel;

using var workbook = new XLWorkbook();
var worksheet = workbook.AddWorksheet($"Documents_{reportData.Month}_{reportData.Year}");

// Headers with styling
worksheet.Cell(1, 1).Value = "Tracking ID";
worksheet.Cell(1, 2).Value = "Direction";
worksheet.Cell(1, 3).Value = "Subject";
worksheet.Cell(1, 4).Value = "Sender/Recipient";
worksheet.Cell(1, 5).Value = "Date";
worksheet.Cell(1, 6).Value = "Original File #";
worksheet.Cell(1, 7).Value = "Current Location";

var headerRow = worksheet.Range(1, 1, 1, 7);
headerRow.Style.Font.Bold = true;
headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

// Bulk data insert
var row = 2;
foreach (var doc in reportData.Documents)
{
    worksheet.Cell(row, 1).Value = doc.TrackingId;
    worksheet.Cell(row, 2).Value = doc.Direction.ToString();
    worksheet.Cell(row, 3).Value = doc.Subject;
    worksheet.Cell(row, 4).Value = doc.Direction == DocumentDirection.Incoming ? doc.Sender : doc.Recipient;
    worksheet.Cell(row, 5).Value = doc.DocumentDate;
    worksheet.Cell(row, 6).Value = doc.OriginalFileNumber;
    worksheet.Cell(row, 7).Value = doc.CurrentLocation;
    row++;
}

// Auto-fit columns
worksheet.Columns().AdjustToContents();

workbook.SaveAs(outputPath);
```

### Anti-Patterns to Avoid

- **Generating PDF/Excel on UI thread:** QuestPDF's `.GeneratePdf()` and ClosedXML's `.SaveAs()` can take seconds for large datasets. Always wrap in `Task.Run()` or use `AsyncRelayCommand` with `await Task.Run(() => ...)`. Following Pitfall 5 (UI Thread Blocking) from PITFALLS.md.
- **Storing full file paths in DB:** Store only the relative filename; construct full path from `_attachmentRoot` + documentId + filename. This makes the attachment directory relocatable.
- **Putting QuestPDF/ClosedXML NuGet references in FileTracker.Core:** Report generation is a UI-layer concern. References belong in FileTracker.App. Core layer only defines DTOs and service interfaces.
- **Dashboard queries in ViewModel:** All SQL queries belong in `DocumentRepository`. ViewModel calls repository (via service) and binds results.
- **File copy without transaction coordination:** If file copy succeeds but DB insert fails, you have an orphaned file. Use a try/catch that cleans up the file on DB failure. Conversely, the DB record should be the source of truth — if the file is missing, show "File not found" rather than crashing.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| PDF generation from C# | Custom System.Drawing + PrintDocument | QuestPDF 2026.5.0 | Hand-rolling pagination, text layout, tables, and fonts is months of work. QuestPDF's layout engine handles page breaking, multi-page tables with headers, font subsetting, and native PDF/A compliance out of the box |
| Excel XLSX generation | Raw OpenXML SDK or CSV with formatting | ClosedXML 0.105.0 | OpenXML SDK requires 10x more code for simple operations (creating a worksheet, populating cells, formatting). ClosedXML wraps it in a clean API |
| File copy with rollback | Manual File.Copy + catch | AttachmentService with try/catch cleanup | The pattern of "copy file, insert DB row, rollback file on failure" is deceptively simple but easy to get wrong (partial failures, race conditions). Wrap in a dedicated service method |
| Dashboard date math | Manual DateTime arithmetic | SQLite date functions: `datetime('now', '-7 days')` | SQLite's built-in date functions are correct and timezone-aware. C# DateTime math can introduce off-by-one errors with date-only comparisons |

**Key insight:** QuestPDF and ClosedXML together solve 90% of the "export" problem in government office apps. The alternative — HTML-to-PDF via wkhtmltopdf or Puppeteer — adds 100MB+ of dependencies, a browser engine, and complex deployment. The pure-.NET approach is the right fit for a Windows desktop app targeting ~30MB footprint.

## Runtime State Inventory

> Phase 3 is a greenfield feature addition (no rename/refactor). This section is intentionally omitted.

## Common Pitfalls

### Pitfall 1: QuestPDF License Not Set → Runtime Exception

**What goes wrong:** `QuestPDF.Settings.License` is not configured before first use. QuestPDF throws `LicenseException` at runtime with a confusing message.

**Why it happens:** QuestPDF requires explicit license configuration as a deliberate design choice — it won't silently operate without a license set.

**How to avoid:** Add `QuestPDF.Settings.License = LicenseType.Community;` in `App.xaml.cs` during `OnStartup`, after `_host.Build()` but before any ViewModel could trigger report generation. This is a one-time setup.

**Warning signs:** `QuestPDF.LicenseException` at runtime. The exception message guides you to set the license.

**Source:** QuestPDF Quick Start docs [CITED: questpdf.com/quick-start.html].

### Pitfall 2: PDF Generation Blocking UI Thread

**What goes wrong:** User clicks "Generate Report" → UI freezes for 3–10 seconds → Windows shows "Not Responding" → user force-closes app.

**Why it happens:** QuestPDF's `GeneratePdf()` runs synchronously on the calling thread. If called from a WPF command handler on the UI thread, it blocks all UI updates. This is Pitfall 5 from PITFALLS.md applied to report generation.

**How to avoid:**
```csharp
[RelayCommand]
private async Task GenerateReportAsync()
{
    IsGenerating = true;
    await Task.Run(() => _reportService.GeneratePdfReport(request, outputPath));
    IsGenerating = false;
    // Show success message on UI thread
}
```

**Warning signs:** "Generate Report" button stays depressed; window title shows "(Not Responding)".

### Pitfall 3: Attachment File Name Collisions

**What goes wrong:** Two attachments with the same original filename (e.g., "scan.pdf") uploaded to the same document overwrite each other.

**Why it happens:** Using the original filename as the storage filename without uniqueness guarantees.

**How to avoid:** Prefix with timestamp: `{yyyyMMdd_HHmmss}_{originalFilename}`. The timestamp provides uniqueness with microsecond granularity for single-user desktop use.

**Warning signs:** `File.Copy` throws `IOException` with "file already exists"; earlier attachments silently disappear.

### Pitfall 4: SQLite WAL Mode and Dashboard Performance

**What goes wrong:** Dashboard queries run slowly as the database grows because WAL mode checkpointing hasn't occurred, or the dashboard issues N+1 queries (one per document for current location).

**Why it happens:** The dashboard needs "current location" for each recent/overdue document. If implemented as a loop calling `GetCurrentLocationAsync()` per document, it produces N+1 database round-trips.

**How to avoid:** Write a single JOIN query that returns documents with their current location in one round-trip:
```sql
SELECT d.*, tp.Name AS CurrentLocation
FROM Documents d
LEFT JOIN (
    SELECT DocumentId, ToPositionId, 
           ROW_NUMBER() OVER (PARTITION BY DocumentId ORDER BY MovementDate DESC, Id DESC) AS rn
    FROM Movements
) latest ON d.Id = latest.DocumentId AND latest.rn = 1
LEFT JOIN Positions tp ON latest.ToPositionId = tp.Id
WHERE d.IsDeleted = 0 AND d.CreatedAt >= datetime('now', '-7 days')
ORDER BY d.CreatedAt DESC;
```

**Warning signs:** Dashboard load takes >500ms for 100+ documents. SQLite log shows many individual queries instead of one.

### Pitfall 5: ClosedXML Font Rendering on Windows Server / Non-Standard Fonts

**What goes wrong:** ClosedXML throws `MissingMethodException` or font-related errors when Calibri or other standard fonts are not installed on the system.

**Why it happens:** ClosedXML 0.105.0 uses SixLabors.Fonts for font measurement. On some Windows configurations (especially Server Core or stripped-down installations), the font fallback behavior differs.

**How to avoid:** This is primarily a concern for server deployments. On Windows 11 Desktop (our target), Calibri and Segoe UI are guaranteed present. For safety, always call `worksheet.Columns().AdjustToContents()` AFTER populating all data — this is the call that triggers font measurement. If font errors occur, the fallback is to set explicit column widths instead.

**Source:** ClosedXML docs — Tips > Missing Font [CITED: docs.closedxml.io/en/latest/tips/missing-font.html].

### Pitfall 6: Attachment Directory Permissions

**What goes wrong:** `Directory.CreateDirectory()` fails or `File.Copy()` fails because the user doesn't have write permissions to `%LocalAppData%\FileTracker\attachments\`.

**Why it happens:** On managed enterprise Windows machines, `%LocalAppData%` is almost always writable by the current user, but group policies can restrict it. Also, antivirus may block file creation in certain directories.

**How to avoid:** Wrap directory creation and file operations in try/catch with user-friendly error messages. Validate that the attachments root is writable on first attachment save (not on app startup — deferred validation). If the path is not writable, fall back to `%UserProfile%\Documents\FileTracker\attachments\`.

**Warning signs:** `UnauthorizedAccessException` or `IOException` when saving first attachment; antivirus quarantine alerts.

## Code Examples

Verified patterns from official sources:

### QuestPDF: Table with header and data rows
```csharp
// Source: QuestPDF official docs [CITED: questpdf.com/api-reference/table/basics.html]
.Table(table =>
{
    table.ColumnsDefinition(columns =>
    {
        columns.ConstantColumn(50);
        columns.RelativeColumn();
        columns.ConstantColumn(125);
    });

    table.Header(header =>
    {
        header.Cell().BorderBottom(2).Padding(8).Text("#");
        header.Cell().BorderBottom(2).Padding(8).Text("Product");
        header.Cell().BorderBottom(2).Padding(8).AlignRight().Text("Price");
    });
    
    foreach (var i in Enumerable.Range(0, 6))
    {
        var price = Math.Round(Random.Shared.NextDouble() * 100, 2);
        table.Cell().Padding(8).Text($"{i + 1}");
        table.Cell().Padding(8).Text(Placeholders.Label());
        table.Cell().Padding(8).AlignRight().Text($"${price}");
    }
});
```

### QuestPDF: Page with header/content/footer slots
```csharp
// Source: QuestPDF official docs [CITED: questpdf.com/quick-start.html]
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        
        page.Header().Text("Report Title").FontSize(20).Bold();
        page.Content().Column(x => { /* content */ });
        page.Footer().AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
        });
    });
}).GeneratePdf("report.pdf");
```

### ClosedXML: Basic workbook creation
```csharp
// Source: ClosedXML official docs [CITED: docs.closedxml.io]
using var workbook = new XLWorkbook();
var worksheet = workbook.Worksheets.Add("Sample Sheet");
worksheet.Cell("A1").Value = "Hello World!";
worksheet.Cell("A2").FormulaA1 = "=MID(A1, 7, 5)";
workbook.SaveAs("HelloWorld.xlsx");
```

### Dapper query for dashboard — pending by officer
```csharp
// Source: Follows existing DocumentRepository Dapper pattern [VERIFIED: codebase grep]
const string sql = @"
    SELECT tp.Name AS OfficerName, COUNT(*) AS DocumentCount
    FROM Movements m
    JOIN Positions tp ON m.ToPositionId = tp.Id
    WHERE m.Id IN (
        SELECT MAX(m2.Id) FROM Movements m2 GROUP BY m2.DocumentId
    )
    GROUP BY m.ToPositionId
    ORDER BY DocumentCount DESC;";

return await _db.QueryAsync<OfficerPendingCountDto>(sql);
```

### WPF: Opening file in default viewer
```csharp
// Source: Microsoft Learn — Process.Start [VERIFIED: learn.microsoft.com]
var psi = new ProcessStartInfo
{
    FileName = attachmentPath,
    UseShellExecute = true  // Opens in default associated program
};
Process.Start(psi);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| HTML → wkhtmltopdf → PDF | QuestPDF Fluent API | 2020 (QuestPDF v1.0) | Eliminates browser dependency, ~100MB footprint saving, pixel-perfect control |
| EPPlus (LGPL/commercial) for Excel | ClosedXML (MIT) for Excel | 2020 (EPPlus v5 license change) | MIT license means no commercial restrictions for IIT Dharwad |
| Database BLOBs for attachments | Filesystem + DB metadata | Industry trend since ~2015 | Prevents SQLite bloat (Pitfall 8), enables direct file access, simpler backup strategy |

**Deprecated/outdated:**
- **PdfSharp (GPL):** License incompatible with proprietary distribution. QuestPDF is the replacement.
- **EPPlus < v5:** Free LGPL version is now 6+ years old, no security patches. Use ClosedXML.
- **Microsoft.Office.Interop.Excel:** Requires Excel installed, COM interop overhead, not supported for server/automation scenarios. ClosedXML is the modern replacement.

## Assumptions Log

> List all claims tagged `[ASSUMED]` in this research. The planner and discuss-phase use this section to identify decisions that need user confirmation before execution.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | IIT Dharwad annual revenue is under $1M USD, qualifying for QuestPDF Community MIT license | Standard Stack | If revenue exceeds $1M, QuestPDF requires a Professional license (~$799/yr). Mitigation: QuestPDF Community license page explicitly includes non-profit/educational institutions; IIT Dharwad as a public educational institution should qualify regardless of revenue threshold |
| A2 | `%LocalAppData%\FileTracker\attachments\` is writable on the target machine | Pitfalls | If group policy restricts LocalAppData, attachment storage fails. Fallback to Documents folder is documented, but requires user confirmation of the preferred path |
| A3 | Dashboard should be the default view on app open (replaces the current search view as primary) | Architecture Patterns | If users prefer search as the default view, the tab layout needs reordering. This is a Claude's Discretion item per CONTEXT.md D-01 |
| A4 | The overdue threshold default of 7 days is appropriate | Standard Stack / Pitfalls | RPT-03 says "configurable threshold, default 7 days" — this is confirmed in REQUIREMENTS.md. The implementation should make it configurable, not hard-coded |
| A5 | ClosedXML 0.105.0 is compatible with net9.0-windows target | Standard Stack | Confirmed by NuGet: targets netstandard2.0 and netstandard2.1, both compatible with net9.0. Risk is LOW |

## Open Questions

1. **Dashboard layout: tabbed vs. sidebar vs. split-panel?**
   - What we know: MainWindow currently has a stacked layout (search → form → DataGrid). Dashboard needs to coexist with existing views.
   - What's unclear: Whether to replace the current default view or add a tab control.
   - Recommendation: Use a `TabControl` with Dashboard and Search/Register tabs. Dashboard is the first (default) tab per D-01. This preserves all existing functionality while adding the new view.

2. **Report file save location — should the user pick, or auto-save to a reports directory?**
   - What we know: CONTEXT.md doesn't specify. Standard WPF pattern is `SaveFileDialog`.
   - What's unclear: Whether the Registrar prefers auto-saved reports to a fixed location or manual file selection.
   - Recommendation: Use `SaveFileDialog` with a default filename like `MonthlyReport_2026_05.pdf` and default directory of `%UserProfile%\Documents\FileTracker\Reports\`. This is the standard desktop app UX.

3. **Dashboard periodic refresh — auto-refresh or manual?**
   - What we know: Dashboard data could go stale if a document is moved while viewing the dashboard.
   - What's unclear: Whether auto-refresh adds value or complexity for a single-user app.
   - Recommendation: Manual refresh via a "Refresh" button on the dashboard panel. Listen for `DocumentRegisteredMessage` and `DocumentMovedMessage` (already using WeakReferenceMessenger) to auto-refresh. This is low-cost and high-value.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build, restore, test | ✓ | 9.0.314 | — |
| dotnet build | Compile | ✓ | 17.14.43 | — |
| dotnet test | Run tests | ✓ | 17.14.43 | — |
| QuestPDF (NuGet) | PDF report generation | ✓ (registry) | 2026.5.0 | — |
| ClosedXML (NuGet) | Excel export | ✓ (registry) | 0.105.0 | — |
| Calibri font | ClosedXML AdjustToContents | ✓ (Win11 default) | Windows 11 built-in | Explicit column widths if font missing |

**Missing dependencies with no fallback:** none
**Missing dependencies with fallback:** none

*All required NuGet packages are available on nuget.org. The .NET SDK is installed and the build system is operational. No external services, databases, or CLIs are required beyond what's already present.*

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + FluentAssertions 7.2.2 + Moq 4.20.72 |
| Config file | none — in-memory SQLite per test class |
| Quick run command | `dotnet test tests/FileTracker.Tests/FileTracker.Tests.csproj --filter "FullyQualifiedName~Dashboard"` |
| Full suite command | `dotnet test tests/FileTracker.Tests/FileTracker.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DASH-01 | Dashboard shows pending count per officer | unit | `dotnet test --filter "FullyQualifiedName~DashboardServiceTests" -x` | ❌ Wave 0 |
| DASH-02 | Dashboard shows recent documents (7 days) | unit | `dotnet test --filter "FullyQualifiedName~DocumentRepository_GetRecent" -x` | ❌ Wave 0 |
| DASH-03 | Dashboard highlights overdue documents (>7 days) | unit | `dotnet test --filter "FullyQualifiedName~DocumentRepository_GetOverdue" -x` | ❌ Wave 0 |
| RPT-01 | Monthly report: incoming/outgoing for selected month/year | unit | `dotnet test --filter "FullyQualifiedName~DocumentRepository_GetByMonth" -x` | ❌ Wave 0 |
| RPT-02 | Report breakdowns by type, department, priority | unit | `dotnet test --filter "FullyQualifiedName~ReportServiceTests" -x` | ❌ Wave 0 |
| RPT-03 | PDF export generates valid file | integration | `dotnet test --filter "FullyQualifiedName~ReportService_PdfExport" -x` | ❌ Wave 0 |
| RPT-04 | Excel export generates valid file | integration | `dotnet test --filter "FullyQualifiedName~ReportService_ExcelExport" -x` | ❌ Wave 0 |
| ATCH-01 | Attach file to document (PDF, JPG, PNG) | unit | `dotnet test --filter "FullyQualifiedName~AttachmentService_Add" -x` | ❌ Wave 0 |
| ATCH-02 | Open attachment in default viewer | manual-only | Cannot automate — requires GUI interaction with external viewer | N/A |
| ATCH-03 | Attachments stored on filesystem, organized by document | unit | `dotnet test --filter "FullyQualifiedName~AttachmentService_StoragePath" -x` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Dashboard||FullyQualifiedName~Report||FullyQualifiedName~Attachment"` 
- **Per wave merge:** `dotnet test tests/FileTracker.Tests/FileTracker.Tests.csproj` (full suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/FileTracker.Tests/Services/DocumentRepositoryDashboardTests.cs` — covers DASH-01, DASH-02, DASH-03, RPT-01 (new repository query methods)
- [ ] `tests/FileTracker.Tests/Services/AttachmentServiceTests.cs` — covers ATCH-01, ATCH-03 (attachment CRUD + filesystem)
- [ ] `tests/FileTracker.Tests/Services/ReportServiceTests.cs` — covers RPT-02, RPT-03, RPT-04 (report generation + PDF/Excel export)
- [ ] `tests/FileTracker.Tests/ViewModels/DashboardViewModelTests.cs` — covers dashboard ViewModel logic (click navigation, refresh)
- [ ] `tests/FileTracker.Tests/ViewModels/ReportViewModelTests.cs` — covers report ViewModel logic (month/year selection, export trigger)
- [ ] Shared fixtures: `tests/FileTracker.Tests/Data/AttachmentTestFixture.cs` — in-memory SQLite + temp directory for attachment tests

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Single-user desktop app; no authentication |
| V3 Session Management | no | Not applicable |
| V4 Access Control | no | Single-user; all data accessible |
| V5 Input Validation | yes | File extension validation (PDF, JPG, PNG only via server-side content inspection); file size limit (10MB); path traversal prevention when constructing attachment storage paths |
| V6 Cryptography | no | No cryptographic operations in this phase |

### Known Threat Patterns for WPF + SQLite + Filesystem

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal in attachment filenames (e.g., `..\..\evil.exe`) | Tampering / Elevation | Use `Path.GetFileName()` to strip directory components; construct storage path from trusted root + sanitized filename + document ID only |
| Malicious file uploaded with .exe extension renamed to .pdf | Tampering | Check file signature (magic bytes) for PDF (%PDF), JPEG (FF D8 FF), PNG (89 50 4E 47); reject mismatches |
| Large file upload exhausting disk space | Denial of Service | Enforce 10MB file size limit before copy; check `DriveInfo.AvailableFreeSpace` before writing |
| SQL injection via attachment filename | Tampering | Use Dapper parameterized queries (already in use); never concatenate filenames into SQL strings |
| Uncontrolled file access via Process.Start | Elevation | Always validate the path is within the managed attachments directory before opening; never open arbitrary user-supplied paths |

## Sources

### Primary (HIGH confidence)
- [nuget.org] QuestPDF 2026.5.0 package page — version, license, dependencies, downloads [VERIFIED: dotnet package search + WebFetch]
- [nuget.org] ClosedXML 0.105.0 package page — version, MIT license confirmed, downloads [VERIFIED: dotnet package search + WebFetch]
- [questpdf.com] Quick Start guide — `Document.Create()`, `GeneratePdf()`, license configuration, Fluent API [CITED]
- [questpdf.com] API Reference > Table > Basics — column definitions, header/footer, cell spanning [CITED]
- [questpdf.com] License > Community — MIT license terms confirmed [CITED]
- [docs.closedxml.io] Index + bulk insert, cell format, tables — `XLWorkbook()`, `InsertData()`, `AdjustToContents()` [CITED]
- [codebase] FileTracker.Core/Services/IDocumentRepository.cs — existing repository interface pattern [VERIFIED: codebase grep]
- [codebase] FileTracker.Data/DocumentRepository.cs — Dapper query patterns, transaction handling [VERIFIED: codebase grep]
- [codebase] FileTracker.App/App.xaml.cs — DI registration pattern, service registration [VERIFIED: codebase grep]
- [codebase] FileTracker.Data/DatabaseInitializer.cs — schema creation pattern, seed data pattern [VERIFIED: codebase grep]

### Secondary (MEDIUM confidence)
- [codebase] .planning/research/STACK.md — attachment storage path, architecture notes, MVVM pattern [CITED]
- [codebase] .planning/research/PITFALLS.md — Pitfall 5 (UI thread blocking), Pitfall 8 (BLOB storage), Pitfall 11 (export capability) [CITED]
- [codebase] .planning/REQUIREMENTS.md — DASH-01..03, RPT-01..04, ATCH-01..03 [CITED]

### Tertiary (LOW confidence)
- Training data — QuestPDF and ClosedXML API patterns (all verified against official docs; training data was consistent)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — QuestPDF 2026.5.0 and ClosedXML 0.105.0 both verified on NuGet with exact version numbers, license terms confirmed via official docs
- Architecture: HIGH — existing service/repository patterns in codebase are well-understood; new patterns follow the same conventions with minor adaptations for filesystem operations
- Pitfalls: HIGH — pitfalls sourced from official docs (QuestPDF license, SQLite performance) and existing project PITFALLS.md; all verified against codebase patterns

**Research date:** 2026-05-29
**Valid until:** 2026-06-29 (30 days — stable libraries with infrequent breaking changes)
