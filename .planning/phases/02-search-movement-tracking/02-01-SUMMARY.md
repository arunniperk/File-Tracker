---
phase: 02-search-movement-tracking
plan: 01
subsystem: database
tags: [dapper, sqlite, wpf, communitytoolkit-mvvm, soft-delete]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: "Document model, repository patterns, DI setup, DatabaseInitializer, in-memory SQLite test infrastructure"
provides:
  - "Position entity with Id, Name, DisplayOrder, IsActive"
  - "IPositionRepository with GetAllAsync, GetActiveAsync, InsertAsync, UpdateAsync, DeactivateAsync (soft-delete)"
  - "IPositionService with AddAsync, RenameAsync, MoveUpAsync, MoveDownAsync, DeactivateAsync"
  - "ManagePositionsWindow UI for CRUD operations on officer positions"
  - "8 default positions seeded (D-05) in Positions table"
affects: [02-03-movement-tracking]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Repository interfaces live in FileTracker.Core.Services namespace (follows IDocumentRepository pattern)"
    - "Soft-delete via IsActive=0 (no hard DELETE), matching D-06"
    - "DisplayOrder swap-based reordering (MoveUp/MoveDown swap with adjacent row)"
    - "Idempotent seed data via WHERE NOT EXISTS (SELECT 1 ... LIMIT 1)"

key-files:
  created:
    - src/FileTracker.Core/Models/Position.cs
    - src/FileTracker.Core/Services/IPositionRepository.cs
    - src/FileTracker.Core/Services/IPositionService.cs
    - src/FileTracker.Core/Services/PositionService.cs
    - src/FileTracker.Data/PositionRepository.cs
    - src/FileTracker.App/ViewModels/ManagePositionsViewModel.cs
    - src/FileTracker.App/Views/ManagePositionsWindow.xaml
    - src/FileTracker.App/Views/ManagePositionsWindow.xaml.cs
    - src/FileTracker.App/Converters/BoolToStatusConverter.cs
    - tests/FileTracker.Tests/Data/PositionRepositoryTests.cs
    - tests/FileTracker.Tests/Services/PositionServiceTests.cs
  modified:
    - src/FileTracker.Data/DatabaseInitializer.cs
    - src/FileTracker.App/App.xaml.cs
    - src/FileTracker.App/ViewModels/MainViewModel.cs
    - src/FileTracker.App/MainWindow.xaml

key-decisions:
  - "Repository interfaces (IPositionRepository) placed in FileTracker.Core.Services rather than FileTracker.Data — follows existing IDocumentRepository pattern to avoid circular dependency (Core cannot reference Data)"
  - "PositionService constructor takes (IPositionRepository, SqliteConnection, ILogger) — matches DocumentService pattern for potential transaction use in future plans"
  - "MoveUp/MoveDown use simple DisplayOrder swap (not gap management) — RESEARCH.md confirms this is sufficient for < 50 positions"

patterns-established:
  - "Position management follows Document CRUD pattern: Core interface → Data implementation, Dapper parameterized SQL, IAsyncLifetime test infrastructure"

requirements-completed: ["MOVE-05"]

# Metrics
duration: 7min
completed: 2026-05-29
---

# Phase 02 Plan 01: Configurable Officer Hierarchy (Position Management) Summary

**Persistent officer hierarchy with soft-delete positions, Dapper/SQLite data layer, and WPF management UI with 26 passing tests**

## Performance

- **Duration:** 7 min
- **Started:** 2026-05-29T14:51:19Z
- **Completed:** 2026-05-29T14:58:20Z
- **Tasks:** 2
- **Files created/modified:** 15 (11 created, 4 modified)

## Accomplishments
- `Positions` table with `Id`, `Name`, `DisplayOrder`, `IsActive` schema and `IX_Positions_DisplayOrder` index
- 8 default officer positions seeded idempotently (Faculty/Department → Director, per D-05)
- `IPositionRepository` with full CRUD + `DeactivateAsync` (soft-delete, row preserved for historical integrity per D-06)
- `IPositionService` with `AddAsync` (sequential DisplayOrder), `RenameAsync` (with validation), `MoveUpAsync`/`MoveDownAsync` (swap-based reordering), `DeactivateAsync`
- `ManagePositionsWindow` with DataGrid showing Order, Name, Status (Active/Inactive), and Actions (Rename, Up, Down, Deactivate)
- 26 new tests covering all repository and service methods, including edge cases (first/last reorder no-ops, empty name validation)
- All 73 tests pass (26 new Position tests + 47 existing Phase 1 tests)

## Task Commits

1. **Task 1: Position model, repository, service, database init, and tests** (TDD)
   - `4261899` — `test(02-01): add failing tests for Position repository and service` (RED)
   - `0005694` — `feat(02-01): implement Position model, repository, service, and database init` (GREEN)
2. **Task 2: ManagePositionsWindow with add, rename, reorder, deactivate UI**
   - `3e2aacb` — `feat(02-01): add ManagePositionsWindow with add, rename, reorder, deactivate UI`

## Files Created/Modified
- `src/FileTracker.Core/Models/Position.cs` — Position entity: Id, Name, DisplayOrder, IsActive
- `src/FileTracker.Core/Services/IPositionRepository.cs` — Repository interface (5 methods, no Delete)
- `src/FileTracker.Core/Services/IPositionService.cs` — Service interface (7 methods)
- `src/FileTracker.Core/Services/PositionService.cs` — Business logic: validation, sequential ordering, swap reorder
- `src/FileTracker.Data/PositionRepository.cs` — Dapper implementation with parameterized SQL
- `src/FileTracker.Data/DatabaseInitializer.cs` — Positions table CREATE + 8 seed positions (idempotent)
- `src/FileTracker.App/ViewModels/ManagePositionsViewModel.cs` — UI state and commands via CommunityToolkit.Mvvm
- `src/FileTracker.App/Views/ManagePositionsWindow.xaml` — WPF window with DataGrid and action buttons
- `src/FileTracker.App/Views/ManagePositionsWindow.xaml.cs` — Code-behind (InitializeComponent only)
- `src/FileTracker.App/Converters/BoolToStatusConverter.cs` — IsActive → "Active"/"Inactive" display
- `src/FileTracker.App/App.xaml.cs` — DI registrations for IPositionRepository, IPositionService, ManagePositionsViewModel
- `src/FileTracker.App/ViewModels/MainViewModel.cs` — OpenManagePositions relay command
- `src/FileTracker.App/MainWindow.xaml` — ⚙ Positions button in registration form area
- `tests/FileTracker.Tests/Data/PositionRepositoryTests.cs` — 9 repository tests (IAsyncLifetime, in-memory SQLite)
- `tests/FileTracker.Tests/Services/PositionServiceTests.cs` — 17 service tests (real repo wired to in-memory DB)

## Decisions Made
- Repository interfaces placed in `FileTracker.Core.Services` (not `FileTracker.Data`) — follows existing `IDocumentRepository` pattern. Core project cannot reference Data project (circular dependency).
- `PositionService` constructor takes `SqliteConnection db` — matches `DocumentService` pattern, available for future transaction-scoped operations.
- MoveUp/MoveDown use simple DisplayOrder swap — RESEARCH.md pattern confirms this is sufficient. No gap management or re-indexing needed for < 50 positions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Moved IPositionRepository from FileTracker.Data to FileTracker.Core.Services**
- **Found during:** Task 1 (GREEN phase implementation)
- **Issue:** Plan specified `IPositionRepository.cs` in `src/FileTracker.Data/`, but `FileTracker.Core` does not reference `FileTracker.Data` (circular dependency). Place `PositionService` in Core referencing `IPositionRepository` from Data → compilation error `CS0246`.
- **Fix:** Created `IPositionRepository` in `src/FileTracker.Core/Services/` (same location as existing `IDocumentRepository`), deleted the Data-level copy, updated `PositionRepository` import.
- **Files modified:** `src/FileTracker.Core/Services/IPositionRepository.cs` (moved), `src/FileTracker.Data/PositionRepository.cs` (added `using FileTracker.Core.Services`), `src/FileTracker.Core/Services/PositionService.cs` (removed `using FileTracker.Data`)
- **Verification:** Build succeeds, all 73 tests pass
- **Committed in:** `0005694` (Task 1 GREEN commit)

---

**Total deviations:** 1 auto-fixed (blocking)
**Impact on plan:** Following pre-existing architectural pattern. No scope change.

## Issues Encountered
- Microsoft.Testing.Platform ignores VSTest `--filter` parameter (warning MTP0001) — all tests run unfiltered. Not a blocker since full suite is fast (3s).

## Known Stubs
- Move Up/Down buttons always visible in Actions column — clicking Up on first row or Down on last row is a no-op (service handles this correctly). Plan suggested hiding buttons conditionally, but the functional behavior is identical. Cosmetic-only gap.

## Threat Flags
None — all threat vectors were already identified in the plan's threat model (T-02-01 through T-02-05). Dapper parameterized SQL prevents injection (T-02-02). Name validation via `string.IsNullOrWhiteSpace` in PositionService blocks empty inputs (T-02-01).

## User Setup Required
None — no external service configuration required. All data persisted to existing SQLite database.

## Next Phase Readiness
- `IPositionService.GetActiveAsync()` returns active positions ordered by DisplayOrder — ready for Plan 02-03 movement dropdowns
- `MoveUpAsync`/`MoveDownAsync` swap logic tested and working — reordering foundation ready
- `DeactivateAsync` soft-delete preserves historical data — ready for movement tracking integrity

---
*Phase: 02-search-movement-tracking*
*Completed: 2026-05-29*
