# Architecture Patterns

**Domain:** File/Document Tracking System (Government Registrar Office)  
**Researched:** 2026-05-29  
**Confidence:** HIGH (Verified against Microsoft official docs, Context7, CommunityToolkit source)

---

## Recommended Architecture

**Pattern:** MVVM + Layered Architecture with Dependency Injection  
**Rationale:** WPF desktop applications naturally separate into Views (XAML), ViewModels (state+bindings), and Models (data). Layering below ViewModels — Services and Repositories — keeps business logic testable and swappable. CommunityToolkit.Mvvm eliminates boilerplate with source generators, and the .NET Generic Host provides first-class DI, configuration, and logging.

```
┌──────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐   │
│  │ Main     │  │ Register │  │ Track    │  │ Report        │   │
│  │ Window   │  │ Window   │  │ Window   │  │ Window        │   │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └───────┬───────┘   │
│       │              │              │                │           │
│  ┌────▼──────────────▼──────────────▼────────────────▼───────┐   │
│  │                    VIEWMODELS                              │   │
│  │  MainViewModel  RegisterVM  TrackVM  SearchVM  ReportVM   │   │
│  │  (ObservableObject + [ObservableProperty] + [RelayCommand])│  │
│  └──────────────────────────┬───────────────────────────────┘   │
└─────────────────────────────┼────────────────────────────────────┘
                              │  (IDocumentService, IReportService)
┌─────────────────────────────▼────────────────────────────────────┐
│                      APPLICATION LAYER (Services)                 │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────┐   │
│  │ DocumentService  │  │ MovementService  │  │ReportService │   │
│  │ Register/Query/  │  │ Transfer/History │  │ Generate/    │   │
│  │ Update/Search    │  │ Status Tracking  │  │ Export       │   │
│  └────────┬─────────┘  └────────┬─────────┘  └──────┬───────┘   │
│           │                     │                    │           │
│  ┌────────▼─────────────────────▼────────────────────▼───────┐   │
│  │                  MESSENGER (WeakReferenceMessenger)       │   │
│  │       Cross-ViewModel events: DocumentRegistered,         │   │
│  │       DocumentMoved, FilterChanged                        │   │
│  └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────┬────────────────────────────────────┘
                              │  (IDocumentRepository, IScanStore)
┌─────────────────────────────▼────────────────────────────────────┐
│                    DATA ACCESS LAYER (Repositories)               │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐  │
│  │ DocumentRepository   │  │ ScanStore (File System)          │  │
│  │ SQLite via           │  │ Local Directory:                 │  │
│  │ sqlite-net-pcl       │  │ %AppData%/FileTracker/Scans/     │  │
│  └──────────────────────┘  └──────────────────────────────────┘  │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    SQLite Database                          │  │
│  │  Documents | Movements | Officers | Config                 │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

---

## Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| **Views (XAML Windows)** | Render UI, capture user input, data binding | ViewModels (via DataContext binding) |
| **ViewModels** | Expose observable state, handle commands, orchestrate service calls | Services (via DI), other VMs (via Messenger) |
| **DocumentService** | Business logic: register, validate, search, update documents | DocumentRepository, ScanStore |
| **MovementService** | Business logic: transfer documents between officers, audit history | DocumentRepository |
| **ReportService** | Query aggregation, report generation | DocumentRepository |
| **DocumentRepository** | CRUD operations on SQLite for documents and movements | SQLite (via sqlite-net-pcl) |
| **ScanStore** | Save/retrieve scanned PDF/images to/from local filesystem | File System |
| **Messenger** | Decoupled cross-component events | ViewModels, Services |
| **App.xaml.cs** | Host setup, DI container configuration, startup | All (via DI registration) |

### Boundary Rules

1. **Views never access Repositories directly.** Only ViewModels talk to Services.
2. **ViewModels hold no persistence logic.** They delegate to Services.
3. **Services hold business rules** (e.g., "a document cannot be moved to an officer below its current position in the hierarchy").
4. **Repositories are pure data access.** No business logic.
5. **Cross-ViewModel communication uses Messenger**, not direct references.

---

## Data Flow

### 1. Registering a New Document

```
User fills form (View)
  → [RelayCommand] SubmitCommand (ViewModel)
    → DocumentService.RegisterAsync(dto) (Service)
      → Validation: required fields, unique file number
      → DocumentRepository.InsertAsync(entity) (Repository)
        → SQLite INSERT
      → ScanStore.SaveAsync(fileBytes, documentId) if attachment
      → Messenger.Send(new DocumentRegisteredMessage(document))
  ← ViewModel refreshes list via ObservableProperty
  ← View updates via data binding
```

### 2. Tracking Document Movement

```
User selects document + target officer (View)
  → [RelayCommand] MoveCommand (ViewModel)
    → MovementService.TransferAsync(docId, fromOfficer, toOfficer)
      → Validate hierarchy rules (no backwards moves unless override)
      → DocumentRepository.GetDocumentAsync(docId)
      → Update Document.CurrentLocation, CurrentOfficer
      → Insert Movement record (from, to, timestamp, remarks)
      → Messenger.Send(new DocumentMovedMessage(docId))
  ← ViewModel updates bound properties
  ← View reflects new status
```

### 3. Generating Monthly Report

```
User selects date range (View)
  → [RelayCommand] GenerateReportCommand (ViewModel)
    → ReportService.GenerateMonthlyAsync(startDate, endDate)
      → DocumentRepository.QueryAsync(filters by date range)
      → Aggregate: incoming count, outgoing count, per-department breakdown
      → Format output (DataTable for display, or PDF/Excel export)
  ← ViewModel populates ReportData ObservableProperty
  ← View renders report grid/chart
```

### 4. Searching Documents

```
User types search query (View)
  → Debounced binding to SearchText (ViewModel)
    → OnSearchTextChanged partial method triggers
    → DocumentService.SearchAsync(query, filters)
      → DocumentRepository.QueryAsync(LINQ with SQLite)
  ← ViewModel.SearchResults updated
  ← View renders filtered list
```

---

## Patterns to Follow

### Pattern 1: MVVM with CommunityToolkit Source Generators

**What:** Use `[ObservableProperty]` on private fields to auto-generate public properties with `INotifyPropertyChanged`. Use `[RelayCommand]` on private methods to auto-generate `ICommand` properties.

**When:** Always for WPF ViewModels. Eliminates hundreds of lines of boilerplate.

**Example:**
```csharp
public partial class RegisterDocumentViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _senderName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _fileNumber = string.Empty;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        // Delegate to service
        await _documentService.RegisterAsync(new DocumentDto
        {
            SenderName = SenderName,
            FileNumber = FileNumber,
            // ...
        });
    }

    private bool CanSubmit() =>
        !string.IsNullOrWhiteSpace(SenderName) &&
        !string.IsNullOrWhiteSpace(FileNumber);
}
```

### Pattern 2: Repository Pattern

**What:** Abstract data access behind an interface. Implementations use sqlite-net-pcl directly.

**When:** All data access. Enables unit testing with in-memory substitutes.

**Example:**
```csharp
public interface IDocumentRepository
{
    Task<Document> GetByIdAsync(int id);
    Task<List<Document>> QueryAsync(Expression<Func<Document, bool>> predicate);
    Task<int> InsertAsync(Document document);
    Task<int> UpdateAsync(Document document);
    Task<List<Document>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<List<Document>> SearchAsync(string query);
}

public class DocumentRepository : IDocumentRepository
{
    private readonly SQLiteAsyncConnection _db;

    public DocumentRepository(SQLiteAsyncConnection db)
    {
        _db = db;
    }

    public async Task<List<Document>> QueryAsync(Expression<Func<Document, bool>> predicate)
    {
        return await _db.Table<Document>().Where(predicate).ToListAsync();
    }

    // ...
}
```

### Pattern 3: Messenger for Cross-Component Events

**What:** Use `WeakReferenceMessenger` to broadcast events that multiple components care about (e.g., "document registered" refreshes search and dashboard simultaneously).

**When:** When one action should update multiple ViewModels. Avoids tight coupling.

**Example:**
```csharp
// Publishing (in DocumentService)
WeakReferenceMessenger.Default.Send(new DocumentRegisteredMessage(document));

// Subscribing (in DashboardViewModel constructor)
WeakReferenceMessenger.Default.Register<DocumentRegisteredMessage>(this, (r, m) =>
{
    // Refresh dashboard counts
    _ = LoadDashboardAsync();
});
```

### Pattern 4: .NET Generic Host in WPF

**What:** Configure DI, logging, and configuration at startup using `Host.CreateApplicationBuilder()`.

**When:** Application entry point (`App.xaml.cs`). Registers all services, ViewModels, and Windows.

**Example:**
```csharp
public partial class App : Application
{
    private IHost _host;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Data
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileTracker", "filetracker.db");
        builder.Services.AddSingleton(new SQLiteAsyncConnection(dbPath));

        // Repositories
        builder.Services.AddSingleton<IDocumentRepository, DocumentRepository>();

        // Services
        builder.Services.AddSingleton<IDocumentService, DocumentService>();
        builder.Services.AddSingleton<IMovementService, MovementService>();
        builder.Services.AddSingleton<IReportService, ReportService>();

        // ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<RegisterDocumentViewModel>();

        // Windows
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        // Initialize database tables
        var db = _host.Services.GetRequiredService<SQLiteAsyncConnection>();
        await db.CreateTableAsync<Document>();
        await db.CreateTableAsync<Movement>();

        _host.Services.GetRequiredService<MainWindow>().Show();
    }
}
```

---

## Data Model (Domain Entities)

### Document
```
Document
├── Id (int, PK, auto-increment)
├── DocumentType (enum: Incoming | Outgoing)
├── FileNumber (string, unique)
├── Subject (string)
├── Sender (string)  — who sent it (for incoming)
├── Recipient (string) — intended recipient
├── Department (string)
├── Priority (enum: Normal | Urgent | Critical)
├── Remarks (string)
├── RegisteredDate (DateTime)
├── CurrentOfficerId (int, FK → Officer)
├── CurrentStatus (enum: Registered | InTransit | AtOfficer | Completed | Archived)
├── HasScan (bool)
└── CreatedAt / UpdatedAt (DateTime)
```

### Movement
```
Movement
├── Id (int, PK)
├── DocumentId (int, FK → Document)
├── FromOfficerId (int, FK → Officer, nullable for initial registration)
├── ToOfficerId (int, FK → Officer)
├── MovedDate (DateTime)
├── Remarks (string)
└── MovementType (enum: Registration | Transfer | Completion | Return)
```

### Officer
```
Officer
├── Id (int, PK)
├── Name (string)
├── Designation (string) — "Registrar", "Dean Admin", etc.
├── HierarchyLevel (int) — 1=Faculty, 2=AR, 3=DR, 4=Registrar, 5=AEE, 6=EE, 7=Dean, 8=Director
└── IsActive (bool)
```

**Hierarchy order (from bottom to top):**
1. Faculty / Departments
2. Assistant Registrar (AR)
3. Deputy Registrar (DR)
4. Registrar
5. Assistant Executive Engineer (AEE)
6. Executive Engineer (EE)
7. Dean Admin
8. Director

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Code-Behind Business Logic

**What:** Putting business logic in XAML code-behind (`Window.xaml.cs`) event handlers.

**Why bad:** Untestable, duplicates logic across windows, violates MVVM.

**Instead:** Move logic to ViewModels via `[RelayCommand]` bindings.

### Anti-Pattern 2: Direct Database Access from ViewModels

**What:** ViewModel instantiates `SQLiteAsyncConnection` and runs queries directly.

**Why bad:** Tight coupling, impossible to unit-test ViewModel, duplicate queries.

**Instead:** All data access through Repository interfaces injected via DI.

### Anti-Pattern 3: God Service

**What:** A single `DataService` that does document CRUD, movement tracking, reporting, search, and file storage.

**Why bad:** Violates single responsibility, hard to test, merge conflicts on large teams.

**Instead:** Separate `DocumentService`, `MovementService`, `ReportService`, each with focused responsibilities.

### Anti-Pattern 4: Mutable Global State

**What:** A static `AppState.CurrentDocument` or `AppState.DocumentList` accessed everywhere.

**Why bad:** Unpredictable mutations, race conditions, impossible to reason about.

**Instead:** Each ViewModel owns its state. Cross-VM sharing via Messenger events or shared services.

### Anti-Pattern 5: Fat Constructors Without DI

**What:** `new MainWindow(new MainViewModel(new DocumentService(new DocumentRepository(new SQLiteAsyncConnection(...)))))`.

**Why bad:** Impossible to maintain, change order of dependencies requires rewriting all construction.

**Instead:** Use .NET Generic Host DI container — register everything, resolve automatically.

---

## Scalability Considerations

| Concern | At 100 Documents | At 10K Documents | At 100K Documents |
|---------|------------------|------------------|-------------------|
| Search | LINQ on SQLite — instant | SQLite with FTS5 (full-text search) | Add indexing on FileNumber, Subject, Date |
| Report gen | In-memory aggregation — fast | Consider pre-aggregated summary tables | Consider date-partitioned queries |
| Scan storage | Flat directory by year | Directory per year/month | Consider compression or archival |
| UI responsiveness | Synchronous is fine | Use async commands + loading indicators | Background tasks with progress reporting |
| Memory | Load all into DataGrid | Virtualized/scroll-based loading | Pagination (skip/take) in queries |

For this project (single-user, registrar office), 10K documents over several years is the realistic upper bound. SQLite handles this effortlessly.

---

## Suggested Build Order (Dependency Graph)

```
Build Wave 1: Foundation (No dependencies)
├── SQLite database schema (CreateTableAsync for Document, Officer, Movement)
├── Data models (Document.cs, Movement.cs, Officer.cs, enums)
├── IDocumentRepository + DocumentRepository (CRUD)
└── App.xaml.cs Host setup (DI container, database init)

Build Wave 2: Core Services (depends on Wave 1)
├── DocumentService (register, search, update)
├── MovementService (transfer, history)
├── ScanStore (file system save/load)
└── ReportService (aggregation queries)

Build Wave 3: ViewModels (depends on Wave 2)
├── MainViewModel (dashboard, navigation)
├── RegisterDocumentViewModel (incoming/outgoing entry)
├── TrackDocumentViewModel (movement, status view)
├── SearchViewModel (search, filter, results)
└── ReportViewModel (monthly report generation)

Build Wave 4: Views (depends on Wave 3)
├── MainWindow.xaml (shell with navigation)
├── RegisterDocumentWindow.xaml (form with validation)
├── TrackDocumentWindow.xaml (movement history grid)
├── SearchWindow.xaml (search bar + results grid)
└── ReportWindow.xaml (date picker + report grid)

Build Wave 5: Integration (depends on Wave 4)
├── Messenger events (DocumentRegistered → refresh dashboard)
├── Keyboard shortcuts, tab order
├── Error handling and user feedback (toasts, validation)
└── Export functionality (print, PDF)
```

**Phase ordering rationale:**
- Data layer first because everything depends on it.
- Services before ViewModels because ViewModels consume services.
- ViewModels before Views because Views bind to ViewModel properties.
- Integration last because it wires cross-cutting concerns that need all components present.

---

## Sources

- [Microsoft Learn: Common Web Application Architectures (Layered Architecture patterns)](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — HIGH confidence (official docs)
- [Microsoft Learn: Architectural Principles (Separation of Concerns, DI, Encapsulation)](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles) — HIGH confidence (official docs)
- [Microsoft Learn: .NET Generic Host in WPF (DI, Configuration, Logging)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder) — HIGH confidence (official docs)
- [Microsoft Learn: CommunityToolkit.Mvvm (MVVM Toolkit, Source Generators)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — HIGH confidence (official docs)
- [Microsoft Learn: ObservableProperty Attribute (Source Generator Details)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty) — HIGH confidence (official docs)
- [Microsoft Learn: Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) — HIGH confidence (official docs)
- [Context7: sqlite-net-pcl (Async API for SQLite in .NET)](https://github.com/praeclarum/sqlite-net) — HIGH confidence (Context7 verified)
- Domain knowledge: Registrar office document workflow hierarchy — MEDIUM confidence (derived from project requirements in PROJECT.md)
