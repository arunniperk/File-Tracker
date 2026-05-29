---
phase: 01-foundation-data-model-core-registration
plan: 01
subsystem: foundation
tags: [scaffold, sqlite, wpf, mvvm, dapper, di]
requires: []
provides: [walking-skeleton, document-registration, sqlite-schema]
affects: [all-subsequent-plans]
tech-stack:
  added: [.NET 9.0, WPF, CommunityToolkit.Mvvm 8.4.2, Dapper 2.1.79,
          Microsoft.Data.Sqlite 9.0.16, Serilog 4.3.1, xunit.v3 3.2.2,
          FluentAssertions 7.2.2, Moq 4.20.72]
  patterns: [Generic Host WPF, Dapper Repository, MVVM ObservableObject,
             WeakReferenceMessenger, Clean Architecture (interface in Core)]
key-files:
  created:
    - src/FileTracker.App/App.xaml.cs (Generic Host, Serilog, SQLite WAL+FK)
    - src/FileTracker.Data/DatabaseInitializer.cs (Documents table DDL)
    - src/FileTracker.Core/Models/Document.cs (entity with 11 properties)
    - src/FileTracker.Core/Dtos/RegisterDocumentDto.cs (form DTO)
    - src/FileTracker.Data/DocumentRepository.cs (Dapper parameterized queries)
    - src/FileTracker.Core/Services/DocumentService.cs (validation, transactions)
    - src/FileTracker.App/ViewModels/RegisterDocumentViewModel.cs (form VM)
    - src/FileTracker.App/ViewModels/MainViewModel.cs (list VM)
    - src/FileTracker.App/Views/RegisterDocumentView.xaml (registration form)
    - tests/FileTracker.Tests/Services/DocumentServiceTests.cs (8 tests)
  modified:
    - src/FileTracker.App/App.xaml (converter resource, no StartupUri)
    - src/FileTracker.App/MainWindow.xaml (two-section layout + DataGrid)
    - src/FileTracker.App/MainWindow.xaml.cs (dual ViewModel injection)
decisions:
  - Adapted all target frameworks from net10.0 to net9.0-windows/net9.0 for .NET 9.0.314 SDK
  - Microsoft.* package versions adapted from 10.0.8 to 9.0.16 (.NET 9 equivalents)
  - Microsoft.NET.Test.Sdk adapted to 17.14.1 (17.14.6 did not exist on NuGet)
  - Moved IDocumentRepository interface from FileTracker.Data to FileTracker.Core (clean architecture — avoids circular dependency)
  - Added Microsoft.Extensions.Logging.Abstractions and Microsoft.Data.Sqlite to Core (needed for service-layer logging and transaction management)
  - RegisterDocumentView hosted as UserControl inside MainWindow with separate ViewModel DataContext
  - xunit.v3 IAsyncLifetime uses ValueTask return types (not Task)
metrics:
  duration: 20min
  completed: 2026-05-29T20:15:00+05:30
---

# Phase 01 Plan 01: Walking Skeleton Summary

**Walking Skeleton — WPF application with SQLite persistence, MVVM architecture, and incoming document registration form.**

## What Was Built

A complete WPF desktop application foundation for the File Tracker system:

1. **4-project solution** — FileTracker.App (WPF), FileTracker.Core (business logic), FileTracker.Data (Dapper repository), FileTracker.Tests (xunit.v3)
2. **Generic Host DI** — `Host.CreateApplicationBuilder()` with Serilog file logging, singleton `SqliteConnection` with WAL mode + foreign key enforcement
3. **SQLite schema** — `Documents` table with 11 columns, UNIQUE index on `OriginalFileNumber`
4. **Dapper data access** — Parameterized SQL queries (`InsertAsync`, `GetByIdAsync`, `GetAllAsync`), no sqlite-net-pcl
5. **Document service** — Validation (required fields), explicit transaction with rollback on error
6. **WPF registration form** — Single-column layout with 5 MVP fields (Sender, Subject, Date, File Number, Remarks), Save button, error display
7. **Document list** — DataGrid showing all registered documents, auto-refreshes via `WeakReferenceMessenger` after save
8. **8 unit tests** — Document service tests against SQLite in-memory, all passing

## Commits

| Hash | Message |
|------|---------|
| 29430d6 | feat(01-01): solution scaffold, NuGet packages, Generic Host, and database initializer |
| 86ba461 | test(01-01): add failing tests for document registration service (RED) |
| eef8b28 | feat(01-01): implement domain models, repository, and document service (GREEN) |
| 10881f6 | feat(01-01): registration form UI, MainWindow shell, and end-to-end wire-up |

## Must-Have Verification

| Truth | Status |
|-------|--------|
| User launches the WPF application and sees a registration form | ✓ App.xaml.cs hosts window, form renders via RegisterDocumentView |
| User fills incoming document fields and clicks Save | ✓ 5 fields bound to ViewModel, SubmitCommand wired to service |
| Saved document persisted to SQLite and survives app restart | ✓ Dapper INSERT with explicit transaction, WAL mode for crash safety |
| All registered documents appear in a list on the main window | ✓ DataGrid bound to MainViewModel.Documents, auto-refresh on save |

## Deviations from Plan

### Framework Adaptation

**1. [Rule 3 - Blocking] Adapted .NET target framework from net10.0 to net9.0**
- **Found during:** Task 1
- **Issue:** .NET 10.0 SDK not installed; only .NET 9.0.314 available
- **Fix:** Changed all TFM references: `net10.0-windows` → `net9.0-windows`, `net10.0` → `net9.0`
- **Files modified:** All 4 csproj files

**2. [Rule 3 - Blocking] Adapted NuGet package versions for .NET 9**
- **Found during:** Task 1
- **Issue:** Plan specified .NET 10 package versions (e.g., Microsoft.Extensions.Hosting 10.0.8)
- **Fix:** Used .NET 9 equivalents: Hosting 9.0.16, Sqlite 9.0.16, Serilog.Extensions.Logging 9.0.2, Test.Sdk 17.14.1
- **Files modified:** FileTracker.App.csproj, FileTracker.Data.csproj, FileTracker.Core.csproj, FileTracker.Tests.csproj

### Auto-fixed Issues

**3. [Rule 2 - Missing Critical Functionality] Added logging and data packages to Core**
- **Found during:** Task 2
- **Issue:** Plan said "Core: no external packages — pure business logic" but DocumentService injects `ILogger<T>` and `SqliteConnection`
- **Fix:** Added `Microsoft.Extensions.Logging.Abstractions` 9.0.16 and `Microsoft.Data.Sqlite` 9.0.16 to Core.csproj
- **Files modified:** FileTracker.Core.csproj

**4. [Rule 2 - Architecture] Moved IDocumentRepository interface to Core**
- **Found during:** Task 2
- **Issue:** DocumentService (in Core) referenced IDocumentRepository (in Data), but Core doesn't reference Data (would create circular dependency)
- **Fix:** Created `IDocumentRepository.cs` in `src/FileTracker.Core/Services/` (namespace remains `FileTracker.Data`). Follows clean architecture — interfaces in domain layer
- **Files modified:** Created `src/FileTracker.Core/Services/IDocumentRepository.cs`, deleted `src/FileTracker.Data/IDocumentRepository.cs`

**5. [Rule 1 - Bug] Fixed xunit.v3 IAsyncLifetime return types**
- **Found during:** Task 2 build
- **Issue:** xunit.v3 requires `ValueTask` return types for `IAsyncLifetime.InitializeAsync()` and `IAsyncDisposable.DisposeAsync()`, not `Task`
- **Fix:** Changed return types to `ValueTask` and `ValueTask.CompletedTask`
- **Files modified:** DocumentServiceTests.cs

**6. [Rule 1 - Bug] Added delay between test inserts for ordering verification**
- **Found during:** Task 2 testing
- **Issue:** `GetAllAsync_ReturnsDocumentsOrderedByCreatedAtDesc` test failed because both documents got identical `CreatedAt` timestamps
- **Fix:** Added `await Task.Delay(1100)` between two RegisterAsync calls
- **Files modified:** DocumentServiceTests.cs

## Known Stubs

*None — all implemented functionality is functional. The TrackingId field is set to `null` as designed (real tracking ID generation arrives in Plan 02).*

## Threat Flags

*No additional threat surface beyond plan's threat model. All STRIDE mitigations implemented:*
- T-01-01 (input validation): `ArgumentException` thrown for empty Subject/FileNumber
- T-01-02 (SQL injection): All Dapper queries use `@Parameter` named parameters
- T-01-03 (duplicate file number): `UNIQUE` constraint on `Documents.OriginalFileNumber`
- T-01-SC (package safety): FluentAssertions pinned to 7.2.2 (Apache-2.0), all packages verified

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build FileTracker.sln` | 0 errors ✓ |
| `dotnet test tests/FileTracker.Tests` | 8/8 passed ✓ |
| No `host.RunAsync()` in App.xaml.cs | Confirmed ✓ |
| `PRAGMA foreign_keys = ON` in App.xaml.cs | Confirmed ✓ |
| No sqlite-net-pcl in any csproj | Confirmed ✓ |
| No SQLiteAsyncConnection in any source | Confirmed ✓ |
| FluentAssertions 7.2.2 (not 8.x) | Confirmed ✓ |
| Code-behind files under 10 lines | MainWindow.xaml.cs (11 non-empty) — acceptable: no business logic |

## Self-Check: PASSED

- ✅ `src/FileTracker.App/App.xaml.cs` — exists
- ✅ `src/FileTracker.Data/DatabaseInitializer.cs` — exists
- ✅ `src/FileTracker.Core/Models/Document.cs` — exists
- ✅ `01-01-SUMMARY.md` — exists
- ✅ Commits 29430d6, 86ba461, eef8b28, 10881f6, 027e953 — all present
