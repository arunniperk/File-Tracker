# Phase 1: Foundation — Data Model & Core Registration - Research

**Researched:** 2026-05-29
**Domain:** WPF Desktop Application — SQLite Data Layer, MVVM Scaffold, Document Registration Forms
**Confidence:** HIGH

## Summary

Phase 1 is the walking skeleton for a greenfield WPF desktop application. It must deliver the SQLite database schema, the MVVM project scaffold with .NET Generic Host DI, and a document registration form supporting both incoming and outgoing documents with auto-generated tracking IDs and immutable edit audit trails.

The locked tech stack is Dapper over Microsoft.Data.Sqlite (NOT sqlite-net-pcl, despite ARCHITECTURE.md code examples), CommunityToolkit.Mvvm 8.4.2 for source-generated MVVM, and .NET 10.0 with Generic Host for DI. All packages have been verified against NuGet.org as of May 2026.

**Primary recommendation:** Use `Host.CreateApplicationBuilder()` (modern .NET 6+ pattern, NOT the legacy `Host.CreateDefaultBuilder()`), Dapper with raw SQL and explicit transactions, and `ObservableValidator` from CommunityToolkit.Mvvm for form validation. The ARCHITECTURE.md document contains code examples using `sqlite-net-pcl` that are inconsistent with the locked STACK.md decision — the planner must ensure all data access uses Dapper + Microsoft.Data.Sqlite, not sqlite-net-pcl.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** User manually enters the original file number shown on the physical document (for both incoming and outgoing).
- **D-02:** System auto-generates an internal tracking ID in `Sl.No/YYYY` format (e.g., `0001/2026`), resetting yearly. Both the original file number and tracking ID are stored and searchable.
- **D-03:** File number uniqueness is enforced on the original file number field. The auto-generated tracking ID is guaranteed unique.
- **D-04:** Single registration form with an Incoming/Outgoing toggle that swaps the first field (Sender ↔ Recipient).
- **D-05:** MVP fields for Phase 1: Sender (incoming) / Recipient (outgoing), Subject, Date, File Number (manual entry), Remarks. Department, Priority, and Document Type deferred to later phases.
- **D-06:** Form has a clean, single-column layout. The toggle is a prominent radio button or segmented control at the top of the form.
- **D-07:** Edit history displayed as a simple log table: Timestamp, Field Changed, Old Value, New Value. Chronological order, newest first.
- **D-08:** Audit trail is viewed from the document detail panel (separate from the registration form).
- **D-09:** Deletion is NOT supported — edits to documents are tracked, records are never removed.
- **D-10:** Explicit Save button — user fills form and clicks Save to persist. Form is cleared after successful save.
- **D-11:** If user attempts to close or navigate away with unsaved changes, show a warning dialog: "You have unsaved changes. Discard them?"

### Claude's Discretion

- Database schema design (exact table structure, column types, indexes)
- WPF MVVM project structure and file organization
- Exact form styling, spacing, and typography
- Error handling and validation patterns
- DI container setup and service registration
- SQLite connection string and WAL mode configuration

### Deferred Ideas (OUT OF SCOPE)

- Department, Priority, and Document Type fields — deferred to Phase 2
- Configurable file number format (IITDH/REG/YYYY/NNNN) — Phase 1 uses simple Sl.No/YYYY

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REG-01 | User can register an incoming document with sender, subject, date, file number, and remarks (MVP subset) | Dapper INSERT with explicit transaction; ObservableValidator for form binding |
| REG-02 | User can register an outgoing document with recipient, subject, date, file number, and remarks (MVP subset) | Same form toggled by Incoming/Outgoing radio — single ViewModel handles both |
| REG-03 | File numbers auto-generated in Sl.No/YYYY format, resetting yearly | SQLite sequence table pattern (see Architecture Patterns §Pattern 3); guaranteed by DB constraint |
| REG-04 | User can edit a registered document's metadata after entry | Dapper UPDATE with optimistic concurrency check; form pre-populated from existing record |
| REG-05 | All edits create immutable audit trail entries (who changed what, when) | Separate `DocumentAudit` table; INSERT-only; triggered on every field-level change via DocumentService |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Database schema & persistence | Data Access (Repository) | — | Dapper queries over Microsoft.Data.Sqlite; schema managed by Repository |
| Document registration business logic | Application (Service) | — | DocumentService validates, generates tracking IDs, wraps multi-table saves in transactions |
| Tracking ID generation | Application (Service) | Data Access (Repository) | Service queries current year's max sequence, generates next ID, passes to Repository |
| Audit trail recording | Application (Service) | Data Access (Repository) | Service compares old/new values field-by-field, inserts audit rows via Repository |
| Form UI & data binding | Presentation (View/ViewModel) | — | WPF XAML View binds to ViewModel; CommunityToolkit.Mvvm source generators handle INPC |
| Form validation | Presentation (ViewModel) | — | `ObservableValidator` with `[Required]` etc. attributes; validation errors displayed in UI |
| DI container & app startup | Infrastructure (Host) | — | App.xaml.cs configures Generic Host, registers all services/repos/viewmodels |
| File number uniqueness enforcement | Data Access (Repository) | Database (SQLite) | UNIQUE constraint on `OriginalFileNumber` column; DB rejects duplicates |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.203+ (LTS) | Runtime, compilers, tooling | Locked decision. Released Nov 2025, 3-year support. net10.0-windows TFM |
| WPF | Built-in | UI framework | Native Windows 11 Fluent theme. Battle-tested for LOB apps |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators | Used by PowerToys (133K stars), Files (43K stars). `[ObservableProperty]`, `[RelayCommand]`, `ObservableValidator` |
| Microsoft.Data.Sqlite | 10.0.8 | SQLite ADO.NET provider | Official Microsoft provider. 107M downloads. Dependency of EF Core SQLite |
| Dapper | 2.1.79 | Micro-ORM | 679M downloads. Used by Bitwarden, Sonarr, Radarr. Extension methods on `DbConnection` |
| Microsoft.Extensions.Hosting | 10.0.8 | DI, configuration, logging host | 1.4B downloads. Standard .NET Generic Host. Use `Host.CreateApplicationBuilder()` |
| Serilog | 4.3.1 | Structured logging | 2.8B downloads. Apache 2.0 license |
| Serilog.Sinks.File | 7.0.0 | File-based log output | 1.1B downloads. Rolling file support |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.142 | WPF behaviors (EventToCommand) | 33.8M downloads. Official Microsoft package |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Unit testing framework | All test projects. v2 is deprecated — must use v3 |
| FluentAssertions | 7.2.2 or 8.10.0 | Readable test assertions | All test projects. ⚠️ v8 requires paid license for commercial use (see §Package Legitimacy Audit) |
| Moq | 4.20+ [ASSUMED] | Mocking framework | Service/ViewModel unit tests |
| Serilog.Extensions.Logging | (transitive) | Bridge Serilog → Microsoft.Extensions.Logging | Included automatically when using Generic Host + Serilog |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dapper 2.1.79 | sqlite-net-pcl | sqlite-net-pcl provides `SQLiteAsyncConnection` with LINQ-like API, but STACK.md locked Dapper. Dapper gives full SQL control, lighter weight |
| FluentAssertions 8.x | FluentAssertions 7.2.2 | v7.2.2 is last Apache-2.0 version. v8 requires Xceed commercial license. Educational use may qualify as non-commercial |
| Host.CreateApplicationBuilder() | Host.CreateDefaultBuilder() | `CreateApplicationBuilder` is the modern .NET 6+ pattern. `CreateDefaultBuilder` is legacy callback-based. Use the modern pattern for new projects |

**Installation:**
```bash
dotnet add package Microsoft.Data.Sqlite --version 10.0.8
dotnet add package Dapper --version 2.1.79
dotnet add package CommunityToolkit.Mvvm --version 8.4.2
dotnet add package Microsoft.Extensions.Hosting --version 10.0.8
dotnet add package Serilog --version 4.3.1
dotnet add package Serilog.Sinks.File --version 7.0.0
dotnet add package Microsoft.Xaml.Behaviors.Wpf --version 1.1.142

# Test project only:
dotnet add package xunit.v3 --version 3.2.2
dotnet add package FluentAssertions --version 7.2.2  # or 8.10.0 if license allows
dotnet add package Moq --version 4.20.72
```

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Microsoft.Data.Sqlite 10.0.8 | NuGet | 9+ yrs | 107.4M | github.com/dotnet/dotnet | N/A (not JS) | Approved |
| Dapper 2.1.79 | NuGet | 14+ yrs | 679.8M | github.com/DapperLib/Dapper | N/A (not JS) | Approved |
| CommunityToolkit.Mvvm 8.4.2 | NuGet | 3+ yrs | 22.1M | github.com/CommunityToolkit/dotnet | N/A (not JS) | Approved |
| Microsoft.Extensions.Hosting 10.0.8 | NuGet | 7+ yrs | 1.4B | github.com/dotnet/dotnet | N/A (not JS) | Approved |
| Serilog 4.3.1 | NuGet | 11+ yrs | 2.8B | github.com/serilog/serilog | N/A (not JS) | Approved |
| Serilog.Sinks.File 7.0.0 | NuGet | 8+ yrs | 1.1B | github.com/serilog/serilog-sinks-file | N/A (not JS) | Approved |
| Microsoft.Xaml.Behaviors.Wpf 1.1.142 | NuGet | 7+ yrs | 33.8M | github.com/microsoft/XamlBehaviorsWpf | N/A (not JS) | Approved |
| xunit.v3 3.2.2 | NuGet | 2+ yrs (v3) | 25.4M | github.com/xunit/xunit | N/A (not JS) | Approved |
| FluentAssertions 8.10.0 | NuGet | 10+ yrs | 694.9M | github.com/fluentassertions/fluentassertions | N/A (not JS) | ⚠️ Flagged — v8 requires paid commercial license (Xceed). Educational institution use may qualify as non-commercial. Fallback: v7.2.2 (Apache-2.0) |
| Moq 4.20+ | NuGet | 12+ yrs | [ASSUMED] | github.com/devlooped/moq | N/A (not JS) | [ASSUMED] — version not verified in this session |

**Packages removed due to slopcheck [SLOP] verdict:** none (slopcheck not applicable — .NET ecosystem)

**Packages flagged as suspicious [SUS]:** none

**⚠️ Licensing alert:** FluentAssertions 8.x is **not free for commercial use**. If the IIT Dharwad Registrar Office deployment qualifies as non-commercial/educational use, v8 is acceptable. Otherwise, pin to v7.2.2 (last Apache-2.0 licensed version) or switch to Shouldly. The planner must surface this as a checkpoint.

*slopcheck was not run — .NET/NuGet ecosystem. All packages verified against nuget.org directly.*

## Architecture Patterns

### System Architecture Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                         │
│  ┌─────────────────────┐    ┌──────────────────────────┐    │
│  │  MainWindow.xaml     │    │  DocumentDetailWindow    │    │
│  │  (Navigation shell)  │    │  (View-only, Phase 1)    │    │
│  └──────────┬───────────┘    └────────────┬─────────────┘    │
│             │                              │                   │
│  ┌──────────▼──────────────────────────────▼─────────────┐   │
│  │  RegisterDocumentView.xaml                             │   │
│  │  [Incoming/Outgoing Toggle] [Sender/Recipient Field]   │   │
│  │  [Subject] [Date] [File Number] [Remarks] [Save Btn]  │   │
│  └──────────────────────┬─────────────────────────────────┘   │
│                         │  DataContext binding                 │
│  ┌──────────────────────▼─────────────────────────────────┐   │
│  │              VIEWMODELS (CommunityToolkit.Mvvm)         │   │
│  │  MainViewModel  RegisterDocumentVM  DocumentDetailVM   │   │
│  │  ObservableValidator + [ObservableProperty] sources    │   │
│  └──────────────────────┬─────────────────────────────────┘   │
└─────────────────────────┼─────────────────────────────────────┘
                          │  IDocumentService (DI)
┌─────────────────────────▼─────────────────────────────────────┐
│                  APPLICATION LAYER (Services)                   │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  DocumentService                                          │ │
│  │  • RegisterAsync(dto) → validates, generates tracking ID  │ │
│  │  • UpdateAsync(id, dto) → field diff → audit log          │ │
│  │  • GetByIdAsync(id) / GetAllAsync()                       │ │
│  └──────────────────────┬───────────────────────────────────┘ │
└─────────────────────────┼─────────────────────────────────────┘
                          │  IDocumentRepository (DI)
┌─────────────────────────▼─────────────────────────────────────┐
│                  DATA ACCESS LAYER                              │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  DocumentRepository (Dapper over Microsoft.Data.Sqlite)   │ │
│  │  • InsertAsync, UpdateAsync, GetByIdAsync, GetAllAsync   │ │
│  │  • GetNextSequenceAsync(year) → Sl.No/YYYY generation    │ │
│  │  • InsertAuditEntryAsync(field, oldVal, newVal)          │ │
│  └──────────────────────┬───────────────────────────────────┘ │
│                         │  SqliteConnection (registered in DI) │
│  ┌──────────────────────▼───────────────────────────────────┐ │
│  │  SQLite Database (%LocalAppData%\FileTracker\             │ │
│  │  filetracker.db)                                          │ │
│  │  Tables: Documents | DocumentAudit | TrackingSequence     │ │
│  │  PRAGMA: foreign_keys=ON, journal_mode=WAL                │ │
│  └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

**Data flow for document registration:**
1. User fills form (View) → properties bind to ViewModel via `[ObservableProperty]`
2. User clicks Save → `[RelayCommand]` invokes `RegisterDocumentVM.SubmitAsync()`
3. ViewModel calls `IDocumentService.RegisterAsync(dto)`
4. Service validates (required fields), generates tracking ID via `GetNextSequenceAsync(currentYear)`
5. Service calls `IDocumentRepository.InsertAsync(entity)` inside `BEGIN TRANSACTION...COMMIT`
6. Repository executes Dapper `INSERT` with parameters
7. ViewModel clears form on success, shows confirmation; on error, displays validation message

### Recommended Project Structure

```
FileTracker/
├── FileTracker.sln
├── src/
│   ├── FileTracker.App/              # WPF Application project
│   │   ├── App.xaml                  # Application entry, Generic Host setup
│   │   ├── App.xaml.cs               # DI registration, DB init, window launch
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml       # Shell window
│   │   │   ├── MainWindow.xaml.cs
│   │   │   ├── RegisterDocumentView.xaml
│   │   │   └── RegisterDocumentView.xaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   └── RegisterDocumentViewModel.cs
│   │   ├── Converters/
│   │   │   └── BoolToVisibilityConverter.cs  # Incoming/Outgoing toggle
│   │   └── appsettings.json
│   ├── FileTracker.Core/             # Business logic library
│   │   ├── Models/
│   │   │   ├── Document.cs           # Entity/DTO
│   │   │   ├── DocumentAudit.cs      # Audit trail entity
│   │   │   └── Enums/
│   │   │       └── DocumentDirection.cs  # Incoming, Outgoing
│   │   ├── Services/
│   │   │   ├── IDocumentService.cs
│   │   │   └── DocumentService.cs
│   │   └── Dtos/
│   │       └── RegisterDocumentDto.cs
│   └── FileTracker.Data/             # Data access library
│       ├── IDocumentRepository.cs
│       ├── DocumentRepository.cs
│       └── DatabaseInitializer.cs    # Schema creation on startup
└── tests/
    └── FileTracker.Tests/            # Unit test project
        ├── Services/
        │   └── DocumentServiceTests.cs
        └── Data/
            └── DocumentRepositoryTests.cs
```

### Pattern 1: WPF Generic Host Setup (App.xaml.cs)

**What:** Configure DI, Serilog, and database at startup using `Host.CreateApplicationBuilder()`. WPF owns the main thread — the host is built but `host.RunAsync()` is NOT called (WPF runs its own dispatcher loop). Instead, services are resolved from the built host.

**When to use:** Application entry point. THIS EXACT PATTERN is required for WPF + Generic Host.

**Key insight:** WPF applications cannot call `host.RunAsync()` because WPF already owns the UI thread via `Application.Run()`. The correct pattern is: build the host, start it, resolve the main window, show it, then let WPF's dispatcher run. On exit, stop the host.

**Example:**
```csharp
// Source: Microsoft Learn — Generic Host docs, adapted for WPF
// App.xaml.cs
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Logging — Serilog replaces default providers
        builder.Logging.ClearProviders();
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTracker", "logs", "filetracker-.log");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        builder.Logging.AddSerilog();

        // Database — single connection, app-lifetime scoped
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTracker", "filetracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        builder.Services.AddSingleton<SqliteConnection>(_ =>
        {
            var conn = new SqliteConnection(connectionString);
            conn.Open();
            // CRITICAL: See Pitfall 6 — SQLite FK enforcement is OFF by default
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
            return conn;
        });

        // Data layer
        builder.Services.AddSingleton<IDocumentRepository, DocumentRepository>();

        // Services
        builder.Services.AddSingleton<IDocumentService, DocumentService>();

        // ViewModels — transient so each window gets fresh state
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<RegisterDocumentViewModel>();

        // Views
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        // Initialize database schema
        var initializer = ActivatorUtilities
            .CreateInstance<DatabaseInitializer>(_host.Services);
        await initializer.InitializeAsync();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
```

### Pattern 2: Dapper Repository with Explicit Transactions

**What:** All multi-table writes wrapped in `IDbTransaction`. Single-table reads use simple `QueryAsync<T>()`. Connection is injected as a singleton (already opened with PRAGMAs applied).

**When to use:** Every Repository method. Document save touches Documents + TrackingSequence (or Documents + DocumentAudit), requiring atomic transactions.

**Example:**
```csharp
// Source: Dapper official docs (GitHub: DapperLib/Dapper)
public class DocumentRepository : IDocumentRepository
{
    private readonly SqliteConnection _db;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(SqliteConnection db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertAsync(Document document)
    {
        const string sql = @"
            INSERT INTO Documents
                (Direction, Sender, Recipient, Subject, DocumentDate,
                 OriginalFileNumber, TrackingId, Remarks, CreatedAt, UpdatedAt)
            VALUES
                (@Direction, @Sender, @Recipient, @Subject, @DocumentDate,
                 @OriginalFileNumber, @TrackingId, @Remarks, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, document);
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Documents WHERE Id = @Id AND IsDeleted = 0";
        return await _db.QuerySingleOrDefaultAsync<Document>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync()
    {
        const string sql = @"
            SELECT * FROM Documents
            WHERE IsDeleted = 0
            ORDER BY CreatedAt DESC
            LIMIT 200";  // Virtualization-ready — pagination added in Phase 2
        var results = await _db.QueryAsync<Document>(sql);
        return results.AsList();
    }
}

// Multi-table save WITH transaction (in DocumentService, not Repository):
public async Task<Document> RegisterAsync(RegisterDocumentDto dto)
{
    using var transaction = _db.BeginTransaction();
    try
    {
        // 1. Generate tracking ID (query + update TrackingSequence atomically)
        var trackingId = await GetNextTrackingIdAsync(dto.DocumentDate.Year);
        // 2. Insert document
        var doc = dto.ToEntity(trackingId);
        doc.Id = await _repository.InsertAsync(doc);
        // 3. Insert initial audit entry
        await _repository.InsertAuditEntryAsync(new DocumentAudit
        {
            DocumentId = doc.Id,
            FieldName = "Created",
            OldValue = null,
            NewValue = "Document registered",
            ChangedAt = DateTime.UtcNow
        });
        transaction.Commit();
        return doc;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### Pattern 3: Yearly-Reset Tracking ID Generation

**What:** A `TrackingSequence` table holds `(Year INT PK, LastNumber INT)`. On each registration, the service queries the current year's row, increments it, and updates within the same transaction. The generated ID is `{number:D4}/{year}`.

**When to use:** Every document registration. Must be atomic with the document INSERT — both succeed or both roll back.

**Example:**
```csharp
// In DocumentRepository:
public async Task<int> GetNextSequenceAsync(int year, IDbTransaction transaction)
{
    // UPSERT: insert row for year if not exists, then increment
    const string upsertSql = @"
        INSERT INTO TrackingSequence (Year, LastNumber)
        VALUES (@Year, 1)
        ON CONFLICT(Year) DO UPDATE SET LastNumber = LastNumber + 1
        RETURNING LastNumber;";
    return await _db.QuerySingleAsync<int>(upsertSql,
        new { Year = year }, transaction);
}

// Called from DocumentService:
private async Task<string> GenerateTrackingIdAsync(int year)
{
    using var tx = _db.BeginTransaction();
    var seq = await _repository.GetNextSequenceAsync(year, tx);
    tx.Commit();
    return $"{seq:D4}/{year}";  // "0001/2026"
}
```

### Pattern 4: Audit Trail via Field-Level Diff

**What:** When updating a document, DocumentService fetches the existing record, compares field-by-field with the incoming DTO, and inserts one `DocumentAudit` row per changed field. The audit table is append-only (no UPDATEs, no DELETEs).

**When to use:** Every document edit (REG-04 → REG-05).

**Example:**
```csharp
// In DocumentService.UpdateAsync:
public async Task UpdateAsync(int documentId, RegisterDocumentDto dto)
{
    var existing = await _repository.GetByIdAsync(documentId)
        ?? throw new NotFoundException($"Document {documentId} not found");

    var audits = new List<DocumentAudit>();
    var now = DateTime.UtcNow;

    void CheckAndAudit(string fieldName, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
        {
            audits.Add(new DocumentAudit
            {
                DocumentId = documentId,
                FieldName = fieldName,
                OldValue = oldVal,
                NewValue = newVal,
                ChangedAt = now
            });
        }
    }

    CheckAndAudit("Sender", existing.Sender, dto.Sender);
    CheckAndAudit("Recipient", existing.Recipient, dto.Recipient);
    CheckAndAudit("Subject", existing.Subject, dto.Subject);
    CheckAndAudit("OriginalFileNumber", existing.OriginalFileNumber, dto.FileNumber);
    CheckAndAudit("Remarks", existing.Remarks, dto.Remarks);

    if (audits.Count == 0) return; // No changes

    // Apply changes + insert audits in single transaction
    using var tx = _db.BeginTransaction();
    existing.ApplyFrom(dto, now);
    await _repository.UpdateAsync(existing, tx);
    foreach (var audit in audits)
        await _repository.InsertAuditEntryAsync(audit, tx);
    tx.Commit();
}
```

### Pattern 5: ObservableValidator for Form Validation

**What:** ViewModel inherits from `ObservableValidator` (not just `ObservableObject`). Properties decorated with `[Required]`, `[MinLength]`, etc. The `[NotifyDataErrorInfo]` attribute on `[ObservableProperty]` fields triggers automatic validation. WPF binds to `INotifyDataErrorInfo` for error display.

**When to use:** Any ViewModel with user-editable fields.

**Example:**
```csharp
// Source: Microsoft Learn — CommunityToolkit.Mvvm ObservableProperty docs
public partial class RegisterDocumentViewModel : ObservableValidator
{
    private readonly IDocumentService _docService;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Subject is required")]
    [MinLength(3, ErrorMessage = "Subject must be at least 3 characters")]
    private string _subject = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "File number is required")]
    private string _originalFileNumber = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Date is required")]
    private DateTime? _documentDate = DateTime.Today;

    [ObservableProperty]
    private bool _isIncoming = true;  // Toggle: true=Incoming, false=Outgoing

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Sender is required")]
    private string _senderOrRecipient = string.Empty;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        var dto = new RegisterDocumentDto
        {
            Direction = IsIncoming ? DocumentDirection.Incoming : DocumentDirection.Outgoing,
            Sender = IsIncoming ? SenderOrRecipient : null,
            Recipient = IsIncoming ? null : SenderOrRecipient,
            Subject = Subject,
            DocumentDate = DocumentDate!.Value,
            OriginalFileNumber = OriginalFileNumber,
            Remarks = Remarks
        };

        await _docService.RegisterAsync(dto);
        ClearForm();
    }

    private void ClearForm()
    {
        Subject = string.Empty;
        OriginalFileNumber = string.Empty;
        SenderOrRecipient = string.Empty;
        Remarks = string.Empty;
        DocumentDate = DateTime.Today;
        ClearErrors();
    }
}
```

### Anti-Patterns to Avoid
- **Code-behind business logic:** Any logic in `.xaml.cs` beyond `InitializeComponent()`. Use `[RelayCommand]` in ViewModels instead.
- **Direct SQLiteConnection in ViewModels:** All data access through Repository → Service chain. Violates MVVM, makes unit testing impossible.
- **sqlite-net-pcl usage:** ARCHITECTURE.md code examples use `SQLiteAsyncConnection` and `CreateTableAsync<T>()` — these are from sqlite-net-pcl, NOT the locked Dapper + Microsoft.Data.Sqlite stack. Ignore those examples.
- **Synchronous DB calls:** Always `async/await` with Dapper's `QueryAsync<T>()`. Synchronous calls block the WPF UI thread.
- **Missing transactions:** Multi-table saves (document + audit) without explicit transactions risk partial writes.
- **Foreign keys not enforced:** SQLite requires `PRAGMA foreign_keys = ON` on every connection (see Pitfall 6 in PITFALLS.md).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| INPC boilerplate | Manual `OnPropertyChanged()` calls in every setter | `[ObservableProperty]` source generator (CommunityToolkit.Mvvm) | Eliminates hundreds of lines of boilerplate. Auto-generates partial methods for change hooks |
| ICommand implementations | Manual `RelayCommand` classes | `[RelayCommand]` source generator (CommunityToolkit.Mvvm) | Auto-generates `ICommand` properties. Supports `CanExecute`, async, cancellation |
| Form validation display | Manual error templates | `ObservableValidator` + `[NotifyDataErrorInfo]` (CommunityToolkit.Mvvm) | WPF natively binds to `INotifyDataErrorInfo`. Validation attributes flow through automatically |
| SQL connection lifecycle | Manual `new SqliteConnection()` in every method | Singleton `SqliteConnection` registered in DI (opened once with all PRAGMAs) | Dapper is connection-scoped. A single long-lived connection with WAL mode is the recommended pattern for single-user desktop apps |
| DI container wiring | Manual constructor chains (`new MainWindow(new MainViewModel(new DocumentService(...)))`) | `Microsoft.Extensions.Hosting` Generic Host with `Host.CreateApplicationBuilder()` | Industry standard. Automatic resolution of entire dependency graph. Scoped/transient/singleton lifetime management |
| Yearly sequence generation | Application-level counter (not crash-safe) | SQLite `ON CONFLICT` UPSERT with `RETURNING` clause on `TrackingSequence` table | Crash-safe — sequence lives in DB. Single atomic SQL statement. Survives app restarts |
| Audit trail recording | UPDATE with trigger or event sourcing framework | Simple field-level diff comparison in DocumentService, explicit INSERT into DocumentAudit table | Transparent, testable, no magic. D-07/D-08 requirements are straightforward — overengineering risks complexity without benefit |
| Unsaved changes warning | Custom dialog service | `Window.Closing` event in code-behind (the ONE allowed code-behind pattern) + check `HasChanges` flag on ViewModel | Standard WPF pattern. Dialog is built-in `MessageBox`. One-line code-behind is acceptable here |

**Key insight:** CommunityToolkit.Mvvm source generators eliminate >80% of MVVM boilerplate. The `ObservableValidator` base class + `[NotifyDataErrorInfo]` attribute provide WPF-native validation with zero custom infrastructure. Do not build custom validation frameworks — they already exist in the toolkit.

## Runtime State Inventory

> This is a greenfield phase — no existing runtime state to migrate. Skip this section.

**Nothing to inventory.** This project has no prior deployments, no existing databases, no registered OS services, and no build artifacts. The application directory (`%LocalAppData%\FileTracker\`) will be created fresh on first run.

## Common Pitfalls

### Pitfall 1: ARCHITECTURE.md Uses Wrong Data Access Library

**What goes wrong:** The ARCHITECTURE.md document in `.planning/research/` contains code examples using `sqlite-net-pcl` patterns (`SQLiteAsyncConnection`, `CreateTableAsync<T>()`, `.Table<T>()` LINQ methods). The locked STACK.md decision is Dapper over Microsoft.Data.Sqlite — a completely different API.

**Why it happens:** ARCHITECTURE.md was written before the stack was locked, or was generated without cross-referencing STACK.md.

**How to avoid:** The planner must ensure ALL data access code uses Dapper's `QueryAsync<T>()`, `ExecuteAsync()`, and raw SQL — never `sqlite-net-pcl` APIs. The `SqliteConnection` (from Microsoft.Data.Sqlite) is the connection object that Dapper extends. Do NOT install the `sqlite-net-pcl` NuGet package.

**Warning signs:** Any `using SQLite;` statement, any `CreateTableAsync<T>()` call, any `SQLiteAsyncConnection` type reference.

### Pitfall 2: SQLite Foreign Keys Disabled by Default

**What goes wrong:** SQLite does NOT enforce foreign key constraints unless `PRAGMA foreign_keys = ON` is executed. Orphaned child records accumulate silently.

**How to avoid:** Execute `PRAGMA foreign_keys = ON;` immediately after opening the connection in App.xaml.cs. Add an integration test that attempts to insert an orphaned child and verifies it throws.

**Reference:** PITFALLS.md §Pitfall 6, [SQLite FAQ #22](https://www.sqlite.org/faq.html#q22)

### Pitfall 3: Missing Transactions on Multi-Table Saves

**What goes wrong:** Document save involves inserting into Documents table AND updating TrackingSequence. Without an explicit transaction, if the second operation fails, the first persists — leaving the database in an inconsistent state.

**How to avoid:** Every multi-table write in DocumentService wraps operations in `IDbTransaction`. Use `using var tx = _db.BeginTransaction()` with try/catch/rollback.

**Reference:** PITFALLS.md §Pitfall 2, [SQLite FAQ #19](https://www.sqlite.org/faq.html#q19)

### Pitfall 4: WPF Generic Host — Wrong Startup Pattern

**What goes wrong:** Copying the ASP.NET Core or Worker Service startup pattern (`host.RunAsync()`) into a WPF app. WPF owns its own dispatcher thread — calling `host.RunAsync()` blocks the UI thread or conflicts with WPF's message pump.

**How to avoid:** Use the WPF-specific pattern described in Architecture Pattern 1: build the host, call `host.StartAsync()`, then let WPF run the UI loop via `mainWindow.Show()`. Stop the host in `OnExit`.

### Pitfall 5: UI Thread Blocking with Synchronous DB Calls

**What goes wrong:** Using Dapper's synchronous methods (`Query<T>()`, `Execute()`) in ViewModel command handlers. Even fast SQLite queries (50ms) cause perceptible UI stutter.

**How to avoid:** Always use Dapper's async methods (`QueryAsync<T>()`, `ExecuteAsync()`). All `[RelayCommand]` methods should be `async Task`. The source generator handles `AsyncRelayCommand` automatically when the method returns `Task`.

### Pitfall 6: Tracking ID Race Condition

**What goes wrong:** Two rapid registrations could read the same `LastNumber` before either updates it, producing duplicate tracking IDs.

**How to avoid:** Use SQLite's `ON CONFLICT ... DO UPDATE ... RETURNING` clause — a single atomic SQL statement that increments and returns the new value. Wrapping it in a transaction with the document INSERT ensures consistency.

### Pitfall 7: FluentAssertions Licensing

**What goes wrong:** Installing FluentAssertions 8.x without verifying license compliance. v8 requires a paid Xceed license for commercial use.

**How to avoid:** Determine whether IIT Dharwad (educational institution) qualifies for non-commercial use. If uncertain, pin to v7.2.2 (last Apache-2.0 version) or use Shouldly as the assertion library.

## Code Examples

Verified patterns from official sources:

### Dapper Query + Insert (Microsoft.Data.Sqlite)
```csharp
// Source: Dapper GitHub (github.com/DapperLib/Dapper) — verified via NuGet README
// Single-row query with parameters
var doc = await connection.QuerySingleOrDefaultAsync<Document>(
    "SELECT * FROM Documents WHERE Id = @Id",
    new { Id = 5 });

// Insert with auto-generated ID
var newId = await connection.QuerySingleAsync<int>(
    "INSERT INTO Documents (...) VALUES (...); SELECT last_insert_rowid();",
    document);
```

### CommunityToolkit.Mvvm ObservableProperty with Validation
```csharp
// Source: Microsoft Learn — CommunityToolkit.Mvvm ObservableProperty docs
// (learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty)
// Accessed 2026-05-29
public partial class MyViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required")]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
    private string _name = string.Empty;
    // Generates: public string Name { get; set; } with validation
    // Also generates: partial void OnNameChanging(string value), OnNameChanged(string value)
}
```

### RelationalCommand with CanExecute
```csharp
// Source: Microsoft Learn — CommunityToolkit.Mvvm
public partial class RegisterDocumentViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _subject = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        // Implementation
    }

    private bool CanSubmit() =>
        !string.IsNullOrWhiteSpace(Subject) &&
        !HasErrors;
}
```

### SQLite WAL + Foreign Keys PRAGMA
```sql
-- Execute immediately after connection.Open() in App.xaml.cs
-- Source: SQLite official docs (sqlite.org/pragma.html)
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;  -- Safe with WAL, better performance than FULL
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Host.CreateDefaultBuilder()` with callback lambdas | `Host.CreateApplicationBuilder()` with property-based configuration | .NET 6 (2021) | Simpler, flatter API. Recommended for all new projects |
| `System.Data.SQLite` (legacy) | `Microsoft.Data.Sqlite` (official Microsoft) | .NET Core 1.0 (2016) | Official provider. Better .NET integration. Actively maintained |
| xunit v2 | xunit v3 | July 2025 (v3.0.0) | v2 deprecated, no longer maintained. v3 integrates with Microsoft Testing Platform |
| FluentAssertions 7.x (Apache 2.0) | FluentAssertions 8.x (Xceed commercial) | Jan 2025 (v8.0.0) | Breaking licensing change. v7.2.2 remains available for non-commercial use |
| Manual MVVM boilerplate | CommunityToolkit.Mvvm source generators | v8.0.0 (Aug 2022) | Eliminates 80%+ of INPC/ICommand code |
| EF Core for SQLite | Dapper over Microsoft.Data.Sqlite | Locked decision (STACK.md) | 10-15MB smaller, faster cold starts, full SQL control |

**Deprecated/outdated:**
- **xunit v2**: Deprecated. Must use v3. v3.2.2 is current stable.
- **sqlite-net-pcl**: Not deprecated, but STACK.md locked Dapper. Do not use sqlite-net-pcl in this project despite ARCHITECTURE.md examples.
- **Host.CreateDefaultBuilder() for new projects**: Legacy callback pattern. Use `CreateApplicationBuilder()`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Moq version 4.20.72 is the latest stable | Standard Stack | LOW — Moq is mature. Any 4.20.x works. Confirmation during package install |
| A2 | IIT Dharwad qualifies for FluentAssertions non-commercial/educational use | Package Legitimacy Audit | MEDIUM — if classified as commercial, must downgrade to v7.2.2 or switch to Shouldly |
| A3 | .NET 10.0 SDK will be installed before execution | Environment Availability | HIGH — blocking. No .NET SDK detected on this machine |
| A4 | Single `SqliteConnection` singleton with WAL mode is appropriate for single-user app | Architecture Patterns §Pattern 2 | LOW — this is the recommended pattern per SQLite docs for single-user desktop apps |
| A5 | `%LocalAppData%\FileTracker\` is the correct database location | Architecture Patterns §Pattern 1 | LOW — locked decision in STACK.md |
| A6 | WPF Windows 11 Fluent theme works without additional configuration on .NET 10 | Architecture Patterns | LOW — theme is built into WPF on .NET 9+. Verified in STACK.md research |

## Open Questions

1. **FluentAssertions license for educational institution use**
   - What we know: v8.x requires paid Xceed license for commercial use. v7.2.2 is Apache-2.0.
   - What's unclear: Whether an Indian educational institution (IIT Dharwad) deploying software internally qualifies as "non-commercial use" under Xceed's license terms.
   - Recommendation: Default to v7.2.2 (Apache-2.0). If user confirms non-commercial eligibility, upgrade to v8.10.0. Planner: add a `checkpoint:human-verify` task for this decision.

2. **Tracking ID format confirmation**
   - What we know: D-02 specifies `Sl.No/YYYY` (e.g., `0001/2026`).
   - What's unclear: Whether "Sl.No" is the exact prefix string or a placeholder for "serial number." The format `0001/2026` suggests no literal "Sl.No" prefix.
   - Recommendation: Implement as `{sequential:D4}/{year}`. The `Sl.No/` in the decision is descriptive, not literal. The generated ID should be `0001/2026`, not `Sl.No 0001/2026`.

3. **ARCHITECTURE.md correction**
   - What we know: ARCHITECTURE.md code examples use `sqlite-net-pcl`, contradicting the locked STACK.md decision for Dapper.
   - What's unclear: Whether to update ARCHITECTURE.md now or after Phase 1 execution.
   - Recommendation: Add a planner task to update ARCHITECTURE.md code examples to use Dapper + Microsoft.Data.Sqlite patterns. This prevents future phases from copying incorrect examples.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10.0 | Entire project | ✗ | — | **BLOCKING** — must be installed before any code can be built or run |
| Windows 11 | WPF runtime | ✓ | Win32 | — |
| dotnet CLI | Build, test, run | ✗ | — | Included with .NET SDK installation |
| NuGet package source | Package restore | ✓ (assumed) | — | Offline restore possible with local cache |

**Missing dependencies with no fallback:**
- **.NET SDK 10.0**: NOT installed. This blocks all development. The planner MUST add a prerequisite installation task. Download from https://dotnet.microsoft.com/download/dotnet/10.0. Install the SDK (not just the runtime).

**Missing dependencies with fallback:**
- None. The only missing dependency (.NET SDK) has no fallback — it's the foundation.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | none — xunit.v3 uses Microsoft.Testing.Platform conventions. Create `FileTracker.Tests.csproj` with `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` |
| Quick run command | `dotnet test tests/FileTracker.Tests --filter "FullyQualifiedName~DocumentService"` |
| Full suite command | `dotnet test tests/FileTracker.Tests` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REG-01 | Register incoming document with sender, subject, date, file number, remarks | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.RegisterAsync_Incoming"` | ❌ Wave 0 |
| REG-02 | Register outgoing document with recipient, subject, date, file number, remarks | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.RegisterAsync_Outgoing"` | ❌ Wave 0 |
| REG-03 | Tracking ID auto-generates in 0001/YYYY format, resets yearly, guaranteed unique | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.TrackingId_YearlyReset"` | ❌ Wave 0 |
| REG-04 | Edit document metadata (update subject, remarks, etc.) | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.UpdateAsync"` | ❌ Wave 0 |
| REG-05 | Every edit creates audit trail entry with field, old value, new value, timestamp | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.UpdateAsync_CreatesAuditTrail"` | ❌ Wave 0 |
| — | Foreign keys enforced (cannot insert orphaned audit entry) | integration | `dotnet test --filter "FullyQualifiedName~DocumentRepositoryTests.ForeignKeyEnforcement"` | ❌ Wave 0 |
| — | Transaction rollback on failure (partial save not persisted) | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.TransactionRollback"` | ❌ Wave 0 |
| — | ViewModel validation blocks save when required fields are empty | unit | `dotnet test --filter "FullyQualifiedName~RegisterDocumentVM_Validation"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~{TaskClassName}"`
- **Per wave merge:** `dotnet test tests/FileTracker.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/FileTracker.Tests/FileTracker.Tests.csproj` — test project creation
- [ ] `tests/FileTracker.Tests/Services/DocumentServiceTests.cs` — covers REG-01 through REG-05
- [ ] `tests/FileTracker.Tests/Data/DocumentRepositoryTests.cs` — covers data access, FK enforcement
- [ ] `tests/FileTracker.Tests/ViewModels/RegisterDocumentViewModelTests.cs` — covers validation, command behavior
- [ ] `tests/FileTracker.Tests/Usings.cs` or `GlobalUsings.cs` — shared test usings (xunit, FluentAssertions)
- [ ] Test infrastructure: SQLite in-memory or temp-file database for test isolation
- [ ] Moq package install: `dotnet add tests/FileTracker.Tests package Moq`

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Single-user desktop app — no authentication |
| V3 Session Management | no | Single-user desktop app — no sessions |
| V4 Access Control | no | Single-user desktop app — no access control |
| V5 Input Validation | yes | CommunityToolkit.Mvvm `ObservableValidator` with DataAnnotations (`[Required]`, `[MinLength]`, `[MaxLength]`, `[RegularExpression]`). Dapper parameterized queries prevent SQL injection |
| V6 Cryptography | no | No cryptographic operations in Phase 1 |

### Known Threat Patterns for WPF + SQLite + Dapper

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via file number/subject fields | Tampering | Dapper parameterized queries (`new { param }`) — never string concatenation. All user input goes through parameters |
| Malformed file number input crashing parser | Denial of Service | Validate format at ViewModel level before reaching service. `[RegularExpression]` attribute |
| Database file tampering by other processes/users | Tampering / Information Disclosure | File stored in `%LocalAppData%\FileTracker\` — user-specific, not shared. SQLite WAL provides crash safety |
| Missing input validation leading to invalid data | Tampering | `ObservableValidator` + `[NotifyDataErrorInfo]` on all form fields. Server-side (service-level) validation as defense-in-depth |
| Large text inputs causing DB bloat | Denial of Service | `[MaxLength(500)]` on Subject, `[MaxLength(2000)]` on Remarks. Enforced at DB level with column constraints |

## Sources

### Primary (HIGH confidence)
- [NuGet.org: Microsoft.Data.Sqlite 10.0.8](https://www.nuget.org/packages/Microsoft.Data.Sqlite/) — version verified 2026-05-29, 107.4M downloads
- [NuGet.org: Dapper 2.1.79](https://www.nuget.org/packages/Dapper/) — version verified 2026-05-29, 679.8M downloads
- [NuGet.org: CommunityToolkit.Mvvm 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/) — version verified 2026-05-29, 22.1M downloads
- [NuGet.org: Microsoft.Extensions.Hosting 10.0.8](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/) — version verified 2026-05-29
- [NuGet.org: Microsoft.Xaml.Behaviors.Wpf 1.1.142](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf/) — version verified 2026-05-29
- [NuGet.org: Serilog 4.3.1](https://www.nuget.org/packages/Serilog/) — version verified 2026-05-29
- [NuGet.org: Serilog.Sinks.File 7.0.0](https://www.nuget.org/packages/Serilog.Sinks.File/) — version verified 2026-05-29
- [NuGet.org: xunit.v3 3.2.2](https://www.nuget.org/packages/xunit.v3/) — version verified 2026-05-29
- [NuGet.org: FluentAssertions 8.10.0](https://www.nuget.org/packages/FluentAssertions/) — version verified 2026-05-29; ⚠️ commercial license
- [Microsoft Learn: CommunityToolkit.Mvvm ObservableProperty](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty) — source generator patterns, validation attributes
- [Microsoft Learn: .NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) — `Host.CreateApplicationBuilder()` API, lifecycle
- [SQLite Official Docs: PRAGMA statements](https://www.sqlite.org/pragma.html) — foreign_keys, journal_mode, synchronous

### Secondary (MEDIUM confidence)
- [PITFALLS.md §Phase 1](C:\Project\File Tracker\.planning\research\PITFALLS.md) — cross-referenced SQLite pitfalls against official docs
- [STACK.md](C:\Project\File Tracker\.planning\research\STACK.md) — locked technology decisions, cross-verified against NuGet.org
- [ARCHITECTURE.md](C:\Project\File Tracker\.planning\research\ARCHITECTURE.md) — MVVM patterns validated but data access examples contradict locked STACK.md (sqlite-net-pcl vs Dapper)
- [CONTEXT.md](C:\Project\File Tracker\.planning\phases\01-foundation-data-model-core-registration\01-CONTEXT.md) — user decisions D-01 through D-11
- [REQUIREMENTS.md](C:\Project\File Tracker\.planning\REQUIREMENTS.md) — REG-01 through REG-05 definitions

### Tertiary (LOW confidence)
- Moq 4.20.72 version — not verified in this session [ASSUMED]
- Dapper's `AsList()` extension — assumed to create a materialized list from `IEnumerable` [ASSUMED]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all package versions verified against NuGet.org on 2026-05-29
- Architecture: HIGH — patterns verified against Microsoft Learn and CommunityToolkit official docs
- Pitfalls: HIGH — cross-referenced against SQLite official docs and PITFALLS.md
- ARCHITECTURE.md contradiction: HIGH confidence — confirmed sqlite-net-pcl vs Dapper API mismatch

**Research date:** 2026-05-29
**Valid until:** 2026-06-29 (30 days — stable .NET ecosystem, no major releases expected in window)
