# Phase 2: Search & Movement Tracking - Research

**Researched:** 2026-05-29
**Domain:** WPF Desktop Application — Document Search with Pagination, Configurable Officer Hierarchy, Append-Only Movement Tracking
**Confidence:** HIGH

## Summary

Phase 2 builds on the Phase 1 walking skeleton (Documents table, DocumentService, registration form, audit trail) to add the three remaining core capabilities: document search with pagination, a configurable officer position hierarchy, and append-only movement tracking. All three capabilities are database-first — no new NuGet packages are required beyond those installed in Phase 1 (Dapper over Microsoft.Data.Sqlite, CommunityToolkit.Mvvm 8.4.2, .NET 10.0).

The search capability follows the established Dapper pattern: a `SearchAsync` method on `DocumentService` that constructs dynamic SQL using Dapper's `DynamicParameters`, combining optional filters (file number, tracking ID, subject, sender/recipient, date range) with AND logic. Pagination uses SQLite's standard `LIMIT @PageSize OFFSET @Offset` — SQLite handles this efficiently for the expected data volume (10K documents over several years).

The officer hierarchy introduces a `Positions` table (name, display order, active flag) — a one-table CRUD system that is configurable entirely from the database, avoiding the hardcoded-hierarchy pitfall documented in PITFALLS.md §Pitfall 3. Position ordering uses an integer `DisplayOrder` column, with active/inactive via an `IsActive` boolean flag.

Movement tracking introduces a `Movements` table that is append-only and immutable (no UPDATE/DELETE repository methods). Each movement records: document ID, from-position, to-position, direction (sent/received), movement date, and optional remarks. The document's current location is a **derived** property — the `to_position_id` of the most recent movement, computed via SQL query — never a mutable column on the Documents table.

**Primary recommendation:** All three capabilities use exactly the same pattern established in Phase 1: Dapper raw SQL with parameterized queries, service-layer business logic wrapping repository calls inside transactions, and CommunityToolkit.Mvvm ViewModels with `[ObservableProperty]` + `[RelayCommand]`. No new architectural patterns are needed — only new tables, new repository methods, new service methods, and new ViewModels.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Search by original file number, tracking ID, subject, sender/recipient, and date range. All fields are optional (AND-combined).
- **D-02:** Results displayed in the existing DataGrid with pagination via SQL LIMIT/OFFSET.
- **D-03:** Search is triggered by a Search button (not live filtering).
- **D-04:** Officers stored in a configurable Positions table (name, display order, active flag). Not hardcoded.
- **D-05:** Default positions: Faculty/Department, Assistant Registrar, Deputy Registrar, Assistant Executive Engineer, Executive Engineer, Registrar, Dean Admin, Director.
- **D-06:** Positions can be added, renamed, reordered, and deactivated (soft-delete). Deactivated positions hidden from dropdowns.
- **D-07:** Each movement records: document ID, from-position, to-position, direction (sent/received), date, remarks.
- **D-08:** Movements are append-only and immutable (no edit/delete). Audit trait built into the movement history.
- **D-09:** Current location = most recent movement's to-position.
- **D-10:** Search bar at top of main window with fields and Search button.
- **D-11:** "Record Movement" button on each document row opens a movement dialog (select position, direction, date, remarks).
- **D-12:** Document detail panel shows full movement history in chronological order.

### the agent's Discretion

- Exact search query construction (parameterized SQL)
- Pagination control design
- Movement dialog exact layout
- Position management UI approach

### Deferred Ideas (OUT OF SCOPE)

- Department, Priority, Document Type fields — deferred from Phase 1, still not in this phase
- Advanced filters (by department, by status) — later phase

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SRCH-01 | User can search documents by file number, subject, sender/recipient, or date range | Dapper `DynamicParameters` for optional/AND-combined filter SQL (see Architecture Patterns §Pattern 2); `SearchViewModel` with ObservableValidator |
| SRCH-02 | User can view full details of any registered document in a read-only detail panel | Existing `DocumentDetailViewModel` already supports this from Phase 1 — extend with movement history display |
| SRCH-03 | User can view the complete movement history of a document (every officer it passed through, with dates) | `Movements` table JOIN with `Positions`; chronological ORDER BY; displayed in DataGrid within DocumentDetailView |
| SRCH-04 | Search results are paginated (not all loaded at once) | SQLite `LIMIT @PageSize OFFSET @Offset`; `SearchViewModel` tracks current page/total pages; WPF pagination controls |
| MOVE-01 | User can record a document movement to an officer from configurable hierarchy | `MovementService.RecordMovementAsync()`; `Positions` table populates dropdown; direction sent/received |
| MOVE-02 | Each movement records the officer, date, direction, and optional remarks | `Movements` table with `FromPositionId`, `ToPositionId`, `Direction`, `MovementDate`, `Remarks` columns |
| MOVE-03 | User can view the current status/location of any document at a glance | Derived property: `SELECT to_position_id FROM Movements WHERE document_id = @Id ORDER BY movement_date DESC LIMIT 1` |
| MOVE-04 | Movement history is append-only and immutable (cannot edit or delete movements) | `IMovementRepository` exposes ONLY `InsertAsync` and query methods — no `UpdateAsync`, no `DeleteAsync`. Table has no UPDATE/DELETE path |
| MOVE-05 | Officer hierarchy is configurable from the database (add/remove/rename positions) | `Positions` table CRUD via `IPositionRepository`; `IsActive` flag for soft-delete; `DisplayOrder` for ordering |

</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Search query construction & execution | Application (Service) | Data Access (Repository) | DocumentService builds dynamic SQL with DynamicParameters; Repository executes with Dapper |
| Pagination logic | Presentation (ViewModel) | Application (Service) | ViewModel tracks page state (current page, page size, total); Service returns count + results |
| Position CRUD (officer hierarchy) | Application (Service) | Data Access (Repository) | PositionService handles validation/enumeration; Repository handles SQL |
| Movement recording | Application (Service) | Data Access (Repository) | MovementService validates, creates movement entity; Repository INSERT-only |
| Current location derivation | Application (Service) | Data Access (Repository) | MovementService.GetCurrentLocationAsync(); single SQL query over Movements |
| Search UI & DataGrid binding | Presentation (View/ViewModel) | — | SearchViewModel exposes search fields, results collection, pagination state; View binds |
| Movement dialog UI | Presentation (View/ViewModel) | — | RecordMovementViewModel handles dialog state; View is a WPF Window/Dialog |
| Position management UI | Presentation (View/ViewModel) | — | ManagePositionsViewModel handles list/add/edit/deactivate; DataGrid-based |

## Standard Stack

### Core (No new packages — all carried forward from Phase 1)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.203+ (LTS) | Runtime, compilers | Locked in Phase 1 |
| WPF | Built-in | UI framework | Locked in Phase 1 |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators | Locked in Phase 1. `ObservableValidator` for search/movement form validation |
| Microsoft.Data.Sqlite | 10.0.8 | SQLite ADO.NET provider | Locked in Phase 1 |
| Dapper | 2.1.79 | Micro-ORM | Locked in Phase 1. `DynamicParameters` for building search queries with optional AND-filters |
| Microsoft.Extensions.Hosting | 10.0.8 | DI, configuration, logging host | Locked in Phase 1 |
| Serilog | 4.3.1 | Structured logging | Locked in Phase 1 |
| Serilog.Sinks.File | 7.0.0 | File-based log output | Locked in Phase 1 |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.142 | WPF EventToCommand | Locked in Phase 1 |

### Supporting (from Phase 1)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Unit testing | Service/repository tests for search logic, movement recording, position CRUD |
| FluentAssertions | 7.2.2 or 8.10.0 | Assertions | Test assertions. ⚠️ v8 licensing — see Phase 1 RESEARCH.md |
| Moq | 4.20.72 [ASSUMED] | Mocking | Service/ViewModel unit tests |

**Installation:** No new packages required. Phase 2 adds only new tables, repositories, services, and ViewModels using the Phase 1 stack.

## Package Legitimacy Audit

> No new packages introduced in this phase. All dependencies are carried forward from Phase 1 where they were audited.

| Package | Registry | Status |
|---------|----------|--------|
| All Phase 1 packages | NuGet | Already audited in 01-RESEARCH.md §Package Legitimacy Audit |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none
*No new package installations required for Phase 2.*

## Architecture Patterns

### System Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                             │
│                                                                       │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  MainWindow.xaml (Shell — updated)                             │  │
│  │  ┌──────────────────┐  ┌───────────────────────────────────┐  │  │
│  │  │ Search Bar        │  │ Document DataGrid                 │  │  │
│  │  │ [File#][Subject]  │  │ ┌──┬──────┬────────┬──────────┐  │  │  │
│  │  │ [TrackingID]      │  │ │  │File# │Subject │Movement  │  │  │  │
│  │  │ [FromDate][ToDate]│  │ │  │     │       │Btn       │  │  │  │
│  │  │ [Search Button]   │  │ │  │     │       │          │  │  │  │
│  │  └──────────────────┘  │ │ └──┴──────┴────────┴──────────┘  │  │
│  │                         │ │ ◀ Page 1 of 5  ▶                │  │
│  │  ┌──────────────────┐  │ └───────────────────────────────────┘  │
│  │  │ Register Form    │  │                                         │
│  │  │ (from Phase 1)   │  │  ┌───────────────────────────────────┐  │
│  │  └──────────────────┘  │  │ Movement Dialog (Window)           │  │
│  │                         │  │ [From Position ▼] [To Position ▼] │  │
│  │                         │  │ [Direction ▼] [Date] [Remarks]    │  │
│  │                         │  │ [Save] [Cancel]                    │  │
│  │                         │  └───────────────────────────────────┘  │
│  └───────────────────────────────┬───────────────────────────────────┘
│                                  │  DataContext binding               │
│  ┌───────────────────────────────▼───────────────────────────────────┐
│  │              VIEWMODELS (CommunityToolkit.Mvvm)                    │
│  │  MainVM  SearchVM  RecordMovementVM  ManagePositionsVM            │
│  │  DocumentDetailVM (extended with movement history)                 │
│  └───────────────────────────────┬───────────────────────────────────┘
└──────────────────────────────────┼────────────────────────────────────┘
                                   │  IDocumentService, IMovementService,
                                   │  IPositionService (DI)
┌──────────────────────────────────▼────────────────────────────────────┐
│                     APPLICATION LAYER (Services)                       │
│  ┌────────────────┐  ┌──────────────────┐  ┌────────────────────┐    │
│  │ DocumentService│  │ MovementService  │  │ PositionService    │    │
│  │ • SearchAsync  │  │ • RecordMovement │  │ • GetAllAsync      │    │
│  │   (extended)   │  │ • GetHistoryAsync│  │ • AddAsync         │    │
│  │                │  │ • GetCurrent     │  │ • UpdateAsync      │    │
│  │                │  │   LocationAsync  │  │ • DeactivateAsync  │    │
│  └───────┬────────┘  └────────┬─────────┘  └─────────┬──────────┘    │
│          │                    │                       │               │
│  ┌───────▼────────────────────▼───────────────────────▼──────────┐   │
│  │           MESSENGER (WeakReferenceMessenger)                   │   │
│  │  Messages: DocumentRegistered, DocumentMoved, SearchExecuted   │   │
│  └────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────┬────────────────────────────────────────┘
                               │  IDocumentRepository, IMovementRepository,
                               │  IPositionRepository (DI)
┌──────────────────────────────▼────────────────────────────────────────┐
│                     DATA ACCESS LAYER (Repositories)                   │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐    │
│  │DocumentRepository│  │MovementRepository│  │PositionRepository│    │
│  │(extended with    │  │• InsertAsync()   │  │• GetAllAsync()   │    │
│  │ SearchAsync,     │  │  ONLY — no update│  │• InsertAsync()   │    │
│  │ CountAsync)      │  │  or delete       │  │• UpdateAsync()   │    │
│  │                  │  │• GetByDocumentId │  │• DeactivateAsync │    │
│  │                  │  │• GetCurrentAsync │  │                  │    │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘    │
│           │                     │                      │              │
│  ┌────────▼─────────────────────▼──────────────────────▼─────────┐   │
│  │                    SQLite Database (WAL mode)                  │   │
│  │  Documents | DocumentAudit | TrackingSequence                 │   │
│  │  Movements | Positions                       ← NEW TABLES     │   │
│  │  PRAGMA: foreign_keys=ON, journal_mode=WAL                    │   │
│  └────────────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────────┘
```

**Data flow for document search:**
1. User enters filter criteria in search bar (View) → properties bind to `SearchViewModel` via `[ObservableProperty]`
2. User clicks Search → `[RelayCommand]` invokes `SearchViewModel.SearchAsync()`
3. ViewModel calls `IDocumentService.SearchAsync(searchDto)` passing the filters
4. Service constructs parameterized SQL with `DynamicParameters`, adding WHERE clauses only for non-empty filters
5. Service calls `IDocumentRepository.SearchAsync(parameters)` and `CountAsync(parameters)` for total count
6. Repository executes two Dapper queries: one for paginated results (`LIMIT/OFFSET`), one for total count
7. ViewModel updates `SearchResults` and `PaginationInfo` → DataGrid updates via binding
8. Messenger sends `SearchExecutedMessage` to notify other components

**Data flow for recording a movement:**
1. User clicks "Record Movement" button on a document row → opens `RecordMovementWindow`
2. Position dropdowns are populated from `IPositionService.GetActivePositionsAsync()`
3. User selects from-position, to-position, direction, date, remarks → clicks Save
4. ViewModel calls `IMovementService.RecordMovementAsync(dto)`
5. Service validates (positions exist, date valid) → wraps INSERT in transaction
6. Repository inserts into Movements table (INSERT only — no UPDATE/DELETE path)
7. Messenger sends `DocumentMovedMessage` → MainViewModel refreshes document list to show updated current location

### Recommended Project Structure (new files only)

```
src/
├── FileTracker.App/
│   ├── ViewModels/
│   │   ├── SearchViewModel.cs              # NEW: search form state + results
│   │   ├── RecordMovementViewModel.cs      # NEW: movement dialog state
│   │   └── ManagePositionsViewModel.cs     # NEW: position CRUD state
│   ├── Views/
│   │   ├── RecordMovementWindow.xaml       # NEW: movement dialog
│   │   └── ManagePositionsWindow.xaml      # NEW: position management
│   └── Converters/                          # (reuse existing converters)
├── FileTracker.Core/
│   ├── Models/
│   │   ├── Movement.cs                     # NEW: movement entity
│   │   ├── Position.cs                     # NEW: position/hierarchy entity
│   │   └── Enums/
│   │       └── MovementDirection.cs        # NEW: Sent, Received
│   ├── Services/
│   │   ├── IMovementService.cs             # NEW
│   │   ├── MovementService.cs              # NEW
│   │   ├── IPositionService.cs             # NEW
│   │   └── PositionService.cs              # NEW
│   └── Dtos/
│       ├── SearchDocumentDto.cs            # NEW: search filter DTO
│       ├── SearchResultDto.cs              # NEW: search result + pagination DTO
│       └── RecordMovementDto.cs            # NEW: movement data DTO
└── FileTracker.Data/
    ├── IMovementRepository.cs              # NEW
    ├── MovementRepository.cs               # NEW (INSERT-only)
    ├── IPositionRepository.cs              # NEW
    └── PositionRepository.cs               # NEW
```

### Pattern 1: Dynamic Parameterized Search Query (Dapper `DynamicParameters`)

**What:** Build a SQL WHERE clause dynamically based on which optional filters the user provides. Dapper's `DynamicParameters` class accumulates parameters and their values. Only non-empty filters add WHERE conditions — all combined with AND.

**When to use:** Every search execution. SRCH-01 requires AND-combined optional filters on file number, tracking ID, subject, sender/recipient, and date range.

**Why this pattern:** It's safe (all values go through Dapper parameters — no SQL injection), it's testable (the SQL string can be inspected in tests), and it's the standard Dapper approach for dynamic queries [VERIFIED: Dapper GitHub README — `DynamicParameters` section].

**Example:**
```csharp
// In DocumentRepository (or DocumentService, since the SQL construction is business logic):
public async Task<(IReadOnlyList<Document> Results, int TotalCount)> SearchAsync(
    SearchDocumentDto filters, SqliteConnection db)
{
    var parameters = new DynamicParameters();
    var conditions = new List<string>();

    // Build WHERE clauses only for non-empty filters
    if (!string.IsNullOrWhiteSpace(filters.OriginalFileNumber))
    {
        conditions.Add("d.OriginalFileNumber LIKE @FileNumber");
        parameters.Add("FileNumber", $"%{filters.OriginalFileNumber.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(filters.TrackingId))
    {
        conditions.Add("d.TrackingId LIKE @TrackingId");
        parameters.Add("TrackingId", $"%{filters.TrackingId.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(filters.Subject))
    {
        conditions.Add("d.Subject LIKE @Subject");
        parameters.Add("Subject", $"%{filters.Subject.Trim()}%");
    }

    if (!string.IsNullOrWhiteSpace(filters.SenderOrRecipient))
    {
        conditions.Add("(d.Sender LIKE @SenderOrRec OR d.Recipient LIKE @SenderOrRec)");
        parameters.Add("SenderOrRec", $"%{filters.SenderOrRecipient.Trim()}%");
    }

    if (filters.FromDate.HasValue)
    {
        conditions.Add("d.DocumentDate >= @FromDate");
        parameters.Add("FromDate", filters.FromDate.Value.ToString("yyyy-MM-dd"));
    }

    if (filters.ToDate.HasValue)
    {
        conditions.Add("d.DocumentDate <= @ToDate");
        parameters.Add("ToDate", filters.ToDate.Value.ToString("yyyy-MM-dd"));
    }

    var whereClause = conditions.Count > 0
        ? "WHERE d.IsDeleted = 0 AND " + string.Join(" AND ", conditions)
        : "WHERE d.IsDeleted = 0";

    // Paginated results query
    var dataSql = $@"
        SELECT d.* FROM Documents d
        {whereClause}
        ORDER BY d.CreatedAt DESC
        LIMIT @PageSize OFFSET @Offset;";

    // Count query for total pages
    var countSql = $"SELECT COUNT(*) FROM Documents d {whereClause};";

    parameters.Add("PageSize", filters.PageSize);
    parameters.Add("Offset", (filters.Page - 1) * filters.PageSize);

    var results = await db.QueryAsync<Document>(dataSql, parameters);
    var totalCount = await db.QuerySingleAsync<int>(countSql, parameters);

    return (results.AsList(), totalCount);
}
```

### Pattern 2: Append-Only Movement Table (Immutable Records)

**What:** The `Movements` table is designed with **only INSERT** paths at the repository level. There are no `UpdateAsync` or `DeleteAsync` methods on `IMovementRepository`. The repository interface exposes only `InsertAsync`, `GetByDocumentIdAsync`, and `GetCurrentLocationAsync`. This enforces D-08 (immutability) at the API level — the compiler prevents accidental mutation.

**When to use:** All movement recording. MOVE-04 requires immutability.

**Why this pattern:** Government/educational document tracking requires immutable audit trails (PITFALLS.md §Pitfall 4). Making the repository INSERT-only enforces this at the architectural level — not just a convention that a developer might forget.

**Example:**
```csharp
// Interface — no Update, no Delete. INSERT only.
public interface IMovementRepository
{
    Task<int> InsertAsync(Movement movement, IDbTransaction? transaction = null);
    Task<IReadOnlyList<Movement>> GetByDocumentIdAsync(int documentId);
    Task<Movement?> GetCurrentLocationAsync(int documentId);
}

// Repository implementation
public class MovementRepository : IMovementRepository
{
    private readonly SqliteConnection _db;

    public MovementRepository(SqliteConnection db) => _db = db;

    public async Task<int> InsertAsync(Movement movement, IDbTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO Movements
                (DocumentId, FromPositionId, ToPositionId, Direction, MovementDate, Remarks, CreatedAt)
            VALUES
                (@DocumentId, @FromPositionId, @ToPositionId, @Direction, @MovementDate, @Remarks, @CreatedAt);
            SELECT last_insert_rowid();";

        return await _db.QuerySingleAsync<int>(sql, new
        {
            movement.DocumentId,
            movement.FromPositionId,
            movement.ToPositionId,
            Direction = movement.Direction.ToString(),
            MovementDate = movement.MovementDate.ToString("yyyy-MM-dd"),
            movement.Remarks,
            CreatedAt = movement.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }, transaction);
    }

    public async Task<IReadOnlyList<Movement>> GetByDocumentIdAsync(int documentId)
    {
        const string sql = @"
            SELECT m.*,
                   fp.Name AS FromPositionName,
                   tp.Name AS ToPositionName
            FROM Movements m
            LEFT JOIN Positions fp ON m.FromPositionId = fp.Id
            JOIN Positions tp ON m.ToPositionId = tp.Id
            WHERE m.DocumentId = @DocumentId
            ORDER BY m.MovementDate, m.Id;";

        var results = await _db.QueryAsync<Movement>(sql, new { DocumentId = documentId });
        return results.AsList();
    }

    public async Task<Movement?> GetCurrentLocationAsync(int documentId)
    {
        // D-09: Current location = most recent movement's to-position
        const string sql = @"
            SELECT m.*, tp.Name AS ToPositionName
            FROM Movements m
            JOIN Positions tp ON m.ToPositionId = tp.Id
            WHERE m.DocumentId = @DocumentId
            ORDER BY m.MovementDate DESC, m.Id DESC
            LIMIT 1;";

        return await _db.QuerySingleOrDefaultAsync<Movement>(sql, new { DocumentId = documentId });
    }
}
```

### Pattern 3: Configurable Position Hierarchy

**What:** The `Positions` table stores an ordered list of officer positions. `DisplayOrder` controls the sequence, `IsActive` controls visibility in dropdowns. Adding, renaming, reordering, and deactivating positions are simple SQL operations — no recompilation needed.

**When to use:** All position dropdowns in movement dialogs and position management UI. MOVE-01, MOVE-05.

**Why this pattern:** Hardcoded hierarchies break when organizations restructure (PITFALLS.md §Pitfall 3). A configurable table decouples the officer hierarchy from the application code — renames, reordering, and deactivations are data changes, not code changes.

**Example:**
```csharp
// Position entity
public class Position
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// IPositionRepository
public interface IPositionRepository
{
    Task<IReadOnlyList<Position>> GetAllAsync();           // All positions (for management UI)
    Task<IReadOnlyList<Position>> GetActiveAsync();        // Active only (for dropdowns)
    Task<int> InsertAsync(Position position);
    Task UpdateAsync(Position position);                   // Rename or reorder
    Task DeactivateAsync(int positionId);                  // Soft-delete (IsActive = 0)
}
```

**Default seed data (D-05):**
```sql
INSERT INTO Positions (Name, DisplayOrder, IsActive) VALUES
    ('Faculty/Department',       1, 1),
    ('Assistant Registrar',      2, 1),
    ('Deputy Registrar',         3, 1),
    ('Assistant Executive Engr', 4, 1),
    ('Executive Engineer',       5, 1),
    ('Registrar',                6, 1),
    ('Dean Admin',               7, 1),
    ('Director',                 8, 1);
```

### Pattern 4: Pagination State in ViewModel

**What:** The `SearchViewModel` tracks pagination state as `[ObservableProperty]` fields: `CurrentPage`, `PageSize`, `TotalCount`, `TotalPages` (computed). Prev/Next commands increment/decrement `CurrentPage` and re-execute the search.

**When to use:** Search results display. SRCH-04.

**Example:**
```csharp
public partial class SearchViewModel : ObservableObject
{
    private readonly IDocumentService _docService;

    [ObservableProperty] private string _searchFileNumber = string.Empty;
    [ObservableProperty] private string _searchTrackingId = string.Empty;
    [ObservableProperty] private string _searchSubject = string.Empty;
    [ObservableProperty] private string _searchSenderRecipient = string.Empty;
    [ObservableProperty] private DateTime? _searchFromDate;
    [ObservableProperty] private DateTime? _searchToDate;

    [ObservableProperty] private ObservableCollection<Document> _searchResults = new();
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pageSize = 20;

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1; // Reset to first page on new search
        await ExecuteSearchAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await ExecuteSearchAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await ExecuteSearchAsync();
        }
    }

    private async Task ExecuteSearchAsync()
    {
        var filters = new SearchDocumentDto
        {
            OriginalFileNumber = SearchFileNumber,
            TrackingId = SearchTrackingId,
            Subject = SearchSubject,
            SenderOrRecipient = SearchSenderRecipient,
            FromDate = SearchFromDate,
            ToDate = SearchToDate,
            Page = CurrentPage,
            PageSize = PageSize
        };

        var (results, totalCount) = await _docService.SearchAsync(filters);
        SearchResults = new ObservableCollection<Document>(results);
        TotalCount = totalCount;

        // Notify computed properties
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
    }
}
```

### Pattern 5: Movement Recording Dialog

**What:** `RecordMovementViewModel` is a self-contained dialog VM. It receives a `Document` via a `LoadAsync(document)` method, loads active positions for dropdowns, and on save creates a `RecordMovementDto` and calls `IMovementService.RecordMovementAsync()`. After save, it sends a `DocumentMovedMessage` via Messenger and closes the dialog.

**When to use:** MOVE-01, MOVE-02, D-11.

**Example:**
```csharp
public partial class RecordMovementViewModel : ObservableValidator
{
    private readonly IMovementService _movementService;
    private readonly IPositionService _positionService;

    [ObservableProperty] private Document? _document;
    [ObservableProperty] private ObservableCollection<Position> _positions = new();

    [ObservableProperty] [NotifyDataErrorInfo] [Required]
    private Position? _selectedFromPosition;

    [ObservableProperty] [NotifyDataErrorInfo] [Required]
    private Position? _selectedToPosition;

    [ObservableProperty] private MovementDirection _direction = MovementDirection.Sent;
    [ObservableProperty] private DateTime _movementDate = DateTime.Today;
    [ObservableProperty] private string _remarks = string.Empty;

    [RelayCommand]
    private async Task LoadAsync(Document document)
    {
        Document = document;
        var positions = await _positionService.GetActivePositionsAsync();
        Positions = new ObservableCollection<Position>(positions);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors || Document is null) return;

        var dto = new RecordMovementDto
        {
            DocumentId = Document.Id,
            FromPositionId = SelectedFromPosition?.Id,
            ToPositionId = SelectedToPosition!.Id,
            Direction = Direction,
            MovementDate = MovementDate,
            Remarks = Remarks
        };

        await _movementService.RecordMovementAsync(dto);
        WeakReferenceMessenger.Default.Send(new DocumentMovedMessage(Document.Id));
        // Close dialog — handled by code-behind or dialog service
    }
}
```

### Anti-Patterns to Avoid
- **Mutable Movements table:** Never add UPDATE/DELETE methods to `IMovementRepository`. The table is append-only per D-08 and PITFALLS.md §Pitfall 4. Any "correction" of a movement should be a new inverse movement entry, not an edit.
- **CurrentLocation as a column on Documents:** Do NOT add a `CurrentLocation` or `CurrentPositionId` column to the Documents table. Current location MUST be derived from the most recent movement row (D-09). A mutable column would drift out of sync with the movement history.
- **Hardcoded position names in C#:** No `enum OfficerPosition`, no `switch` on position name. Positions are data, not code (PITFALLS.md §Pitfall 3).
- **Live filtering on every keystroke:** D-03 explicitly requires a Search button. Do NOT implement `OnSearchTextChanged` → auto-search. The search is triggered by a button click.
- **Loading all documents**: Phase 1 capped `GetAllAsync()` at 200 rows. Search results MUST use LIMIT/OFFSET pagination per SRCH-04 — never load all matching rows at once.
- **String concatenation for SQL:** Never build WHERE clauses with `$"WHERE column = '{userInput}'"`. Always use Dapper parameters (`@Param` with `DynamicParameters`). The existing Phase 1 codebase already follows this rule — maintain it.
- **Movement without transaction:** Recording a movement is a single-row INSERT and doesn't require a multi-table transaction. However, if future requirements add audit/logging alongside movement recording, wrap in a transaction.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dynamic SQL WHERE clause | String concatenation with user input | Dapper `DynamicParameters` with parameterized SQL (`@Param` placeholders) | Prevents SQL injection. Dapper handles parameter binding safely. Already established in Phase 1 |
| Pagination state tracking | Custom pagination framework | Simple `[ObservableProperty]` fields (`CurrentPage`, `PageSize`, `TotalCount`) + computed `TotalPages` | Single-user app with small datasets — no need for a pagination library. WPF DataGrid handles the display |
| Position dropdown binding | Manual ComboBox population in code-behind | `ObservableCollection<Position>` bound via `ItemsSource` in XAML, `DisplayMemberPath="Name"` | Standard WPF ComboBox binding. ViewModel owns the list, View binds to it |
| Movement immutability enforcement | Convention ("we just won't call update") | Repository interface that exposes only `InsertAsync` — no `UpdateAsync`, no `DeleteAsync` | Compiler-enforced at the API level. A developer cannot accidentally mutate movements |
| Window/dialog lifecycle | Custom dialog service | Direct `new Window { Owner = mainWindow }.ShowDialog()` from ViewModel command, or Messenger signal to code-behind | Single-user WPF app — no need for a dialog abstraction layer. Simple pattern already used for DocumentDetailWindow |
| Current location display | Mutable column on Documents table | SQL query: `ORDER BY movement_date DESC LIMIT 1` to derive current location from Movements | D-09 explicitly requires derivation from movement history. Prevents drift (PITFALLS.md §Pitfall 4) |
| Position reordering | Drag-and-drop UI framework | Simple up/down arrow buttons that swap `DisplayOrder` values and re-query | MVP. Drag-and-drop in DataGrid adds complexity with no user benefit for 8 positions |

**Key insight:** The Phase 1 patterns (Dapper parameterized SQL, CommunityToolkit.Mvvm source generators, Messenger for cross-VM events, explicit Save button, `ObservableValidator` for form validation) are directly applicable to all three Phase 2 capabilities. Do not introduce new patterns or abstractions. The search query, movement recording, and position management are all variations of the same MVVM + Dapper + Service pattern already established.

## Runtime State Inventory

> Phase 2 is greenfield for its domain — Movements and Positions tables do not yet exist. However, existing Phase 1 tables must be preserved.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | Documents table with Phase 1 test data in `%LocalAppData%\FileTracker\filetracker.db` | None — existing Documents remain unchanged. New tables added alongside |
| Live service config | None — no external services | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | Phase 1 compiled binaries in `src/` | None — incremental build handles new files |

**Nothing found requiring data migration.** The `DatabaseInitializer.InitializeAsync()` will be extended to `CREATE TABLE IF NOT EXISTS` for `Movements` and `Positions` — existing data is not affected. Default position seed data should be inserted only if the Positions table is empty (idempotent initialization).

## Common Pitfalls

### Pitfall 1: SQLite LIKE With Leading Wildcard Performance

**What goes wrong:** Search by file number or subject uses `LIKE '%searchterm%'` with leading `%`. On a table with 10K+ documents, this can cause full table scans because SQLite cannot use indexes with leading wildcards.

**Why it happens:** SQLite B-tree indexes are prefix-optimized. A `LIKE '%term%'` pattern cannot use the index because the search term could appear anywhere in the string.

**How to avoid:** For the expected data volume (single-user registrar office, ~10K documents over several years), LIKE with leading wildcard is fine — SQLite scans are fast on this scale. However, if performance becomes a concern in the future, SQLite FTS5 (full-text search) can be added as a migration. For MVP: use LIKE, measure performance, and flag FTS5 as a future optimization.

**Warning signs:** Search operations taking >1 second. This is unlikely below 50K rows.

**Reference:** PITFALLS.md §Pitfall 14 (DataGrid performance with growing data), SQLite official docs — [FTS5](https://www.sqlite.org/fts5.html)

### Pitfall 2: Date Range Boundary Errors

**What goes wrong:** Documents with `DocumentDate` exactly equal to the search's `FromDate` or `ToDate` are excluded because of off-by-one or time-component comparison errors. SQLite stores dates as TEXT in `yyyy-MM-dd` format — string comparison works correctly for equality and range, BUT the application must ensure the database values are formatted consistently.

**Why it happens:** Mixing `DateTime` with time components in C#, or using incorrect comparison operators (`>` instead of `>=`).

**How to avoid:** The existing Phase 1 codebase already stores `DocumentDate` as `yyyy-MM-dd` (string format, no time component) — this is correct and consistent. Search queries use `>= @FromDate` and `<= @ToDate` with the same format. Ensure the `SearchDocumentDto` passes date strings in `yyyy-MM-dd` format. Verify with a test: insert a document on `2026-01-31`, search with `FromDate=2026-01-31`, `ToDate=2026-01-31` — it must be included.

**Reference:** PITFALLS.md §Pitfall 13 (Date handling without time component)

### Pitfall 3: Orphaned Movements After Document Deletion

**What goes wrong:** Documents are soft-deleted (`IsDeleted=1`) but movements are not. The movement history query returns movements for deleted documents, confusing the UI.

**Why it happens:** The Movements table has a FOREIGN KEY to Documents but the FK doesn't cascade soft-deletes (and shouldn't, since movements are append-only evidence).

**How to avoid:** Movement queries should JOIN Documents and filter `WHERE d.IsDeleted = 0`. Deleted documents' movements remain in the database for audit purposes but are excluded from display. The DocumentDetailView already filters by `IsDeleted=0` via the existing `GetByIdAsync` query — if a document is soft-deleted, its detail view won't open, so its movements won't be shown anyway.

**Reference:** PITFALLS.md §Pitfall 4 (No immutable audit trail)

### Pitfall 4: Position Deactivation Breaking Movement History

**What goes wrong:** A position is deactivated (`IsActive=0`) but historical movements reference that position's ID. The movement history display shows "Position #5" instead of "Assistant Registrar" because the JOIN fails or the name is not retrieved.

**Why it happens:** Position queries for dropdowns filter by `IsActive=1` (correct for the "select a position" UI), but the same query is accidentally used for movement history display.

**How to avoid:** Separate queries:
- `GetActiveAsync()` — `WHERE IsActive = 1 ORDER BY DisplayOrder` — for dropdowns (movement dialog, position management)
- `GetByDocumentIdAsync()` — JOINs all positions INCLUDING inactive ones, because historical movements must show the original position name

The movement history query (Pattern 2) should LEFT JOIN or JOIN without the `IsActive` filter so that deactivated positions still display by name. If a position is ever hard-deleted (which should not happen — use deactivation only), the movement still displays the `FromPositionId`/`ToPositionId` value.

**Reference:** PITFALLS.md §Pitfall 3 (Hard-coded officer hierarchy — same principle applies to position lifecycle)

### Pitfall 5: Concurrent Movement Recording Race Condition

**What goes wrong:** Two rapid movement recordings for the same document could create inconsistent "current location" state if the "current location" is derived from `MAX(Id)` and two INSERTs happen in quick succession.

**Why it happens:** SQLite in WAL mode allows concurrent reads and writes, but the single-connection singleton pattern used in Phase 1 serializes all writes.

**How to avoid:** The singleton `SqliteConnection` pattern from Phase 1 already serializes all database access — only one thread's command runs at a time. This is appropriate for a single-user desktop app. The "current location" is derived from `ORDER BY MovementDate DESC, Id DESC LIMIT 1` — this is deterministic even with rapid inserts because the singleton connection processes them sequentially.

**Reference:** Phase 1 RESEARCH.md §Architecture Patterns §Pattern 1 (singleton SqliteConnection)

### Pitfall 6: CTask/Memory from Messenger Subscriptions

**What goes wrong:** `SearchViewModel` or `RecordMovementViewModel` subscribes to `DocumentRegisteredMessage` or `DocumentMovedMessage` via `WeakReferenceMessenger` but the ViewModel is created as `Transient` — when the View is closed, the VM is collected but the messenger registration must also be cleaned up.

**Why it happens:** Although `WeakReferenceMessenger` uses weak references (the VM can be garbage collected even if still registered), it's good practice to unregister explicitly.

**How to avoid:** ViewModels that subscribe to messages should implement `IRecipient<T>` (like `MainViewModel` already does). CommunityToolkit.Mvvm's `WeakReferenceMessenger` handles cleanup automatically when the recipient is garbage collected. For `Transient` ViewModels (like `RecordMovementViewModel`), this is sufficient. For long-lived singletons, no explicit cleanup is needed either — the weak reference prevents leaks.

**Reference:** PITFALLS.md §Pitfall 10 (WPF memory leaks from event subscriptions), but mitigated by `WeakReferenceMessenger`.

## Code Examples

Verified patterns from existing codebase and official sources:

### DynamicParameters for Optional Filter Search
```csharp
// Source: Dapper GitHub README — DynamicParameters section [VERIFIED]
// Adapted for File Tracker search pattern
public async Task<(IReadOnlyList<Document> Results, int TotalCount)> SearchAsync(
    SearchDocumentDto filters, SqliteConnection db)
{
    var parameters = new DynamicParameters();
    var conditions = new List<string>();

    if (!string.IsNullOrWhiteSpace(filters.OriginalFileNumber))
    {
        conditions.Add("d.OriginalFileNumber LIKE @FileNumber");
        parameters.Add("FileNumber", $"%{filters.OriginalFileNumber.Trim()}%");
    }
    // ... additional filters ...

    var whereClause = conditions.Count > 0
        ? "WHERE d.IsDeleted = 0 AND " + string.Join(" AND ", conditions)
        : "WHERE d.IsDeleted = 0";

    var dataSql = $@"
        SELECT d.* FROM Documents d
        {whereClause}
        ORDER BY d.CreatedAt DESC
        LIMIT @PageSize OFFSET @Offset;";

    parameters.Add("PageSize", filters.PageSize);
    parameters.Add("Offset", (filters.Page - 1) * filters.PageSize);

    var results = await db.QueryAsync<Document>(dataSql, parameters);
    var totalCount = await db.QuerySingleAsync<int>(
        $"SELECT COUNT(*) FROM Documents d {whereClause}", parameters);

    return (results.AsList(), totalCount);
}
```

### SQLite Positions Table Schema
```sql
-- Source: Following DatabaseInitializer pattern from Phase 1 GetByDocumentIdAsync code
CREATE TABLE IF NOT EXISTS Positions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    DisplayOrder INTEGER NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS IX_Positions_DisplayOrder
ON Positions(DisplayOrder);
```

### SQLite Movements Table Schema
```sql
-- Source: Following DocumentAudit table pattern from Phase 1
CREATE TABLE IF NOT EXISTS Movements (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DocumentId INTEGER NOT NULL,
    FromPositionId INTEGER,
    ToPositionId INTEGER NOT NULL,
    Direction TEXT NOT NULL CHECK(Direction IN ('Sent', 'Received')),
    MovementDate TEXT NOT NULL,
    Remarks TEXT,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (DocumentId) REFERENCES Documents(Id),
    FOREIGN KEY (FromPositionId) REFERENCES Positions(Id),
    FOREIGN KEY (ToPositionId) REFERENCES Positions(Id)
);

CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId
ON Movements(DocumentId);

CREATE INDEX IF NOT EXISTS IX_Movements_DocumentId_Date
ON Movements(DocumentId, MovementDate);
```

### Messenger Message for Movement
```csharp
// Following DocumentRegisteredMessage pattern from RegisterDocumentViewModel.cs
public class DocumentMovedMessage : ValueChangedMessage<int>
{
    public DocumentMovedMessage(int documentId) : base(documentId) { }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hardcoded `enum OfficerPosition` | Configurable `Positions` table with DisplayOrder | Phase 2 design | No recompilation for hierarchy changes — aligns with PITFALLS.md §Pitfall 3 |
| Current location as mutable column | Derived from `MAX(MovementDate)` query | Phase 2 design | Prevents drift between current location and movement history — aligns with PITFALLS.md §Pitfall 4 |
| Live search on keystroke | Explicit Search button (D-03) | Phase 2 decision | Matches government record lookup UX — clean, predictable, no surprises |
| `GetAllAsync()` with hard 200-row limit | Search with LIMIT/OFFSET pagination | Phase 2 | Scalable for growing document count while `GetAllAsync()` cap remains for initial load |

**Deprecated/outdated:**
- None — Phase 2 introduces new capabilities, not replacements.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Dapper's `DynamicParameters` is available in version 2.1.79 (already installed) | Architecture Patterns §Pattern 1 | LOW — `DynamicParameters` has been part of Dapper since v1.x. The NuGet package is already installed and verified in Phase 1 |
| A2 | SQLite `LIKE '%term%'` with leading wildcard will perform adequately for the expected data volume (~10K documents) | Common Pitfalls §Pitfall 1 | LOW-MEDIUM — if the registrar office has >50K documents, search may become slow. Mitigation: FTS5 can be added later without schema changes to Documents table |
| A3 | `LIMIT @PageSize OFFSET @Offset` works correctly with SQLite via Dapper parameter binding | Architecture Patterns §Pattern 4 | LOW — Dapper standard feature. Verified in Dapper documentation and existing codebase patterns |
| A4 | The existing `SqliteConnection` singleton pattern serializes writes sufficiently for the append-only movement table | Common Pitfalls §Pitfall 5 | LOW — this is the documented pattern for single-user desktop SQLite apps. WAL mode handles concurrent read/write |
| A5 | Default position names (D-05) match the current IIT Dharwad hierarchy | Architecture Patterns §Pattern 3 | LOW — positions are configurable from the database. Even if the defaults are slightly wrong, they can be corrected through the position management UI without code changes |
| A6 | `WeakReferenceMessenger` cleanup is sufficient for transient ViewModels without explicit unsubscription | Common Pitfalls §Pitfall 6 | LOW — documented behavior of CommunityToolkit.Mvvm. Weak references allow GC of recipients |
| A7 | Moq version 4.20.72 is available (not reverified in this session) | Standard Stack | LOW — carried forward from Phase 1 assumption A1 |

## Open Questions

1. **Movement direction semantics for initial registration**
   - What we know: D-07 specifies each movement records a direction (sent/received). The first movement of a document has no prior position to "send from."
   - What's unclear: For an incoming document's first movement (from Registrar desk to Faculty), is this "Sent" (from registrar) or "Received" (by faculty)? Should the initial registration itself create a movement entry?
   - Recommendation: The initial registration does NOT create a movement. The first movement is the first time the document is forwarded. The from-position is the current holder, to-position is the destination. Direction "Sent" means the document left the from-position. If there's no prior movement, `FromPositionId` is NULL. This is handled by the `FromPositionId INTEGER` (nullable) column. Planner: add this as a `checkpoint:human-verify` task to confirm with the Registrar office.

2. **Position reordering mechanism**
   - What we know: D-06 says positions can be reordered. `DisplayOrder` is the ordering column.
   - What's unclear: Should reordering use a simple swap (up/down arrows) or a drag-and-drop UI? How should `DisplayOrder` gaps be handled?
   - Recommendation: MVP uses simple up/down buttons in the ManagePositionsWindow. Clicking "Move Up" swaps `DisplayOrder` with the previous position. Clicking "Move Down" swaps with the next. No gap management needed — just swap the two values. This is simple, predictable, and sufficient for 8 positions. Drag-and-drop is deferred.

3. **Search result display — which columns in the DataGrid?**
   - What we know: Search results are displayed in the existing DataGrid with pagination (D-02).
   - What's unclear: Which columns should the search results DataGrid show? Should it be the same columns as the main document list, or include current location?
   - Recommendation: Show the same columns as the main document list PLUS the current location (derived from the most recent movement). This aligns with MOVE-03 (view current status at a glance). Columns: Tracking ID, File Number, Subject, Direction, Date, Current Location. The planner should confirm with the user.

4. **SQLite FTS5 consideration**
   - What we know: LIKE with leading wildcard works for MVP-scale data.
   - What's unclear: Whether FTS5 should be plumbed into the architecture now (even if not enabled) or deferred entirely.
   - Recommendation: Defer FTS5 entirely. The search repository method's signature (`SearchAsync(SearchDocumentDto)`) is abstract enough that the implementation can be swapped to FTS5 later without affecting ViewModels or Services. The planner should note this as a future optimization path.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10.0 | Build, test, run | ✗ (see Phase 1) | — | BLOCKING — must be installed per Phase 1 RESEARCH.md |
| Windows 11 | WPF runtime | ✓ | Win32 | — |
| SQLite (via Microsoft.Data.Sqlite) | Database | ✓ | Bundled in NuGet | — |
| Dapper 2.1.79 | Data access | ✓ | Installed in Phase 1 | — |
| CommunityToolkit.Mvvm 8.4.2 | MVVM | ✓ | Installed in Phase 1 | — |

**Missing dependencies with no fallback:**
- **.NET SDK 10.0**: Same as Phase 1. Must be resolved before any code can be built.

**Missing dependencies with fallback:**
- None. All Phase 2 dependencies are already satisfied from Phase 1.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | none — uses existing `FileTracker.Tests.csproj` from Phase 1 with `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` |
| Quick run command | `dotnet test tests/FileTracker.Tests --filter "FullyQualifiedName~MovementService"` |
| Full suite command | `dotnet test tests/FileTracker.Tests` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SRCH-01 | Search by file number returns matching documents (parameterized, no injection) | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_ByFileNumber"` | ❌ Wave 0 |
| SRCH-01 | Search with all fields empty returns all (non-deleted) documents | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_NoFilters"` | ❌ Wave 0 |
| SRCH-01 | Search with multiple AND-combined filters narrows results | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_MultipleFilters"` | ❌ Wave 0 |
| SRCH-01 | Search by date range includes boundary dates (inclusive) | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_DateRange"` | ❌ Wave 0 |
| SRCH-04 | Search returns only the requested page of results | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_Pagination"` | ❌ Wave 0 |
| SRCH-04 | Total count reflects all matching documents (not just current page) | unit | `dotnet test --filter "FullyQualifiedName~DocumentServiceTests.SearchAsync_TotalCount"` | ❌ Wave 0 |
| MOVE-01 | Recording a movement stores from-position, to-position, direction, date, remarks | unit | `dotnet test --filter "FullyQualifiedName~MovementServiceTests.RecordMovementAsync"` | ❌ Wave 0 |
| MOVE-02 | Movement entity has all required fields populated after recording | unit | `dotnet test --filter "FullyQualifiedName~MovementServiceTests.MovementFieldsPopulated"` | ❌ Wave 0 |
| MOVE-03 | Current location query returns the most recent movement's to-position | unit | `dotnet test --filter "FullyQualifiedName~MovementRepositoryTests.GetCurrentLocation"` | ❌ Wave 0 |
| MOVE-04 | MovementRepository has no Update or Delete methods (compiler-enforced) | compilation | N/A — verified by interface definition | ❌ Wave 0 |
| MOVE-04 | Foreign key prevents orphaned movement (invalid DocumentId) | integration | `dotnet test --filter "FullyQualifiedName~MovementRepositoryTests.ForeignKeyEnforcement"` | ❌ Wave 0 |
| MOVE-05 | Position can be added via PositionRepository.InsertAsync | unit | `dotnet test --filter "FullyQualifiedName~PositionServiceTests.AddPosition"` | ❌ Wave 0 |
| MOVE-05 | Position can be deactivated (IsActive=0) — still exists but hidden from dropdowns | unit | `dotnet test --filter "FullyQualifiedName~PositionServiceTests.DeactivatePosition"` | ❌ Wave 0 |
| MOVE-05 | Active position query excludes deactivated positions | unit | `dotnet test --filter "FullyQualifiedName~PositionRepositoryTests.GetActive"` | ❌ Wave 0 |
| MOVE-05 | Movement history displays deactivated position names (not just IDs) | unit | `dotnet test --filter "FullyQualifiedName~MovementRepositoryTests.HistoryIncludesInactivePositions"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~{TaskClassName}"`
- **Per wave merge:** `dotnet test tests/FileTracker.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/FileTracker.Tests/Services/DocumentServiceSearchTests.cs` — covers SRCH-01, SRCH-04
- [ ] `tests/FileTracker.Tests/Services/MovementServiceTests.cs` — covers MOVE-01, MOVE-02
- [ ] `tests/FileTracker.Tests/Services/PositionServiceTests.cs` — covers MOVE-05
- [ ] `tests/FileTracker.Tests/Data/MovementRepositoryTests.cs` — covers MOVE-03, MOVE-04 (FK enforcement)
- [ ] `tests/FileTracker.Tests/Data/PositionRepositoryTests.cs` — covers MOVE-05 (active/inactive filtering)
- [ ] `tests/FileTracker.Tests/ViewModels/SearchViewModelTests.cs` — covers pagination logic, command behavior
- [ ] `tests/FileTracker.Tests/ViewModels/RecordMovementViewModelTests.cs` — covers validation, command behavior
- [ ] Test infrastructure: SQLite in-memory or temp-file database for test isolation (may reuse from Phase 1)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Single-user desktop app — no authentication |
| V3 Session Management | no | Single-user desktop app — no sessions |
| V4 Access Control | no | Single-user desktop app — no access control |
| V5 Input Validation | yes | Dapper parameterized queries (DynamicParameters) for search SQL — prevents SQL injection. `ObservableValidator` on movement form fields. Date validation at service layer |
| V6 Cryptography | no | No cryptographic operations in Phase 2 |

### Known Threat Patterns for Phase 2 Features

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via search field inputs | Tampering | Dapper parameterized queries (`DynamicParameters`). No string concatenation with user input. Every filter value goes through `@Param` placeholder |
| SQL injection via position name editing | Tampering | Dapper parameterized queries. Position names are parameterized in INSERT/UPDATE |
| Malformed date strings in search | Denial of Service | Date picker controls at UI level restrict to valid dates. Service layer validates `DateTime` type before building SQL |
| Excessive page size causing memory pressure | Denial of Service | Cap `PageSize` at 100 in the ViewModel. Service can enforce a maximum |
| Negative OFFSET or zero/negative LIMIT | Tampering / DoS | Validate `Page >= 1` and `PageSize > 0` in service before constructing SQL |
| Movement recorded with invalid position IDs | Tampering | Service validates that `FromPositionId` and `ToPositionId` exist in Positions table before INSERT. FK constraint as defense-in-depth |
| Movement date in the future (accidental) | Information Disclosure (misleading audit trail) | Service validates `MovementDate <= DateTime.Today`. Optionally allow override but warn |

## Sources

### Primary (HIGH confidence)
- [Dapper GitHub README (DapperLib/Dapper)](https://github.com/DapperLib/Dapper) — `DynamicParameters`, `QueryAsync<T>()`, parameterized SQL patterns. Verified 2026-05-29
- [Microsoft Learn: CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — `ObservableValidator`, `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger`, `IRecipient<T>`. Verified 2026-05-29
- [SQLite Official Docs: Core Functions (LIKE, GLOB)](https://www.sqlite.org/lang_corefunc.html) — LIKE pattern matching, SQL functions. Verified 2026-05-29
- [Phase 1 RESEARCH.md](C:\Project\File Tracker\.planning\phases\01-foundation-data-model-core-registration\01-RESEARCH.md) — locked stack, architecture patterns, DI setup, anti-patterns carried forward
- [Existing source files](C:\Project\File Tracker\src\FileTracker.Data\DocumentRepository.cs) — Dapper INSERT/UPDATE/query patterns. Verified by code inspection 2026-05-29
- [Existing source files](C:\Project\File Tracker\src\FileTracker.Data\DatabaseInitializer.cs) — CREATE TABLE pattern. Verified by code inspection 2026-05-29
- [Existing source files](C:\Project\File Tracker\src\FileTracker.App\App.xaml.cs) — DI registration pattern. Verified by code inspection 2026-05-29

### Secondary (MEDIUM confidence)
- [PITFALLS.md §Pitfall 2](C:\Project\File Tracker\.planning\research\PITFALLS.md) — SQLite INSERT performance without transactions (mitigated by singleton connection + WAL)
- [PITFALLS.md §Pitfall 3](C:\Project\File Tracker\.planning\research\PITFALLS.md) — Hard-coded hierarchy (mitigated by Positions table design)
- [PITFALLS.md §Pitfall 4](C:\Project\File Tracker\.planning\research\PITFALLS.md) — No immutable audit trail (mitigated by append-only Movements table)
- [PITFALLS.md §Pitfall 14](C:\Project\File Tracker\.planning\research\PITFALLS.md) — DataGrid performance with growing data (mitigated by pagination)
- [STACK.md](C:\Project\File Tracker\.planning\research\STACK.md) — Locked technology decisions
- [ARCHITECTURE.md](C:\Project\File Tracker\.planning\research\ARCHITECTURE.md) — MVVM + Layered Architecture patterns (note: examples use sqlite-net-pcl — ignore, follow Phase 1 Dapper patterns)
- [01-CONTEXT.md](C:\Project\File Tracker\.planning\phases\01-foundation-data-model-core-registration\01-CONTEXT.md) — Phase 1 decisions carried forward
- [02-CONTEXT.md](C:\Project\File Tracker\.planning\phases\02-search-movement-tracking\02-CONTEXT.md) — Phase 2 user decisions D-01 through D-12
- [REQUIREMENTS.md](C:\Project\File Tracker\.planning\REQUIREMENTS.md) — SRCH-01..04 and MOVE-01..05 definitions

### Tertiary (LOW confidence)
- Moq 4.20.72 version — not reverified in this session [ASSUMED — carried forward from Phase 1 A1]
- SQLite LIKE performance at scale — based on general SQLite knowledge, not benchmarked for this specific schema [ASSUMED]
- IIT Dharwad exact officer hierarchy names — D-05 provides defaults; may differ slightly from actual titles [ASSUMED — but configurable, so low risk]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages. All carry forward from Phase 1 where they were verified against NuGet.org
- Architecture: HIGH — patterns are direct extensions of Phase 1 patterns. Dapper `DynamicParameters` and SQLite pagination are well-established techniques
- Pitfalls: HIGH — cross-referenced against PITFALLS.md (which was itself verified against SQLite official docs and Microsoft Learn)
- Search query construction: HIGH — Dapper's `DynamicParameters` pattern is documented in the official README and is the standard approach for dynamic queries

**Research date:** 2026-05-29
**Valid until:** 2026-06-29 (30 days — stable .NET ecosystem, no major releases expected in window)
