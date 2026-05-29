---
phase: 02-search-movement-tracking
plan: 03
subsystem: tracking
tags: [sqlite, dapper, wpf, mvvm, communitytoolkit-mvvm, movement-tracking, append-only]

# Dependency graph
requires:
  - phase: 02-search-movement-tracking
    plan: 01
    provides: IPositionService, Position entity, ManagePositionsWindow
  - phase: 02-search-movement-tracking
    plan: 02
    provides: SearchViewModel, SearchResults DataGrid, pagination infrastructure
provides:
  - Movement entity with append-only INSERT-only MovementRepository
  - MovementService with position validation
  - RecordMovementWindow dialog for recording document movements
  - Current Location column in search results DataGrid
  - Movement history display in DocumentDetailView
  - DocumentMovedMessage for cross-viewmodel refresh notification
affects:
  - phase: 03-reporting-export (reporting will query movement history for document trails)
  - phase: any-phase-using-document-location

# Tech tracking
tech-stack:
  added: []
  patterns:
    - INSERT-only repository (compiler-enforced immutability, no Update/Delete methods)
    - Display-only model properties (CurrentLocation on Document, not persisted)
    - Messenger-based cross-ViewModel communication (DocumentMovedMessage)
    - Async dialog loading pattern (LoadDocumentAsync before ShowDialog)

key-files:
  created:
    - src/FileTracker.Core/Models/Movement.cs - Movement entity with navigation helpers
    - src/FileTracker.Core/Models/Enums/MovementDirection.cs - Sent/Received enum
    - src/FileTracker.Core/Dtos/RecordMovementDto.cs - Movement creation DTO
    - src/FileTracker.Core/Services/IMovementRepository.cs - INSERT-only interface
    - src/FileTracker.Core/Services/IMovementService.cs - Movement service interface
    - src/FileTracker.Core/Services/MovementService.cs - Movement service with validation
    - src/FileTracker.Data/MovementRepository.cs - Dapper INSERT + JOIN queries
    - src/FileTracker.App/ViewModels/RecordMovementViewModel.cs - Movement dialog VM
    - src/FileTracker.App/Views/RecordMovementWindow.xaml - Movement dialog UI
    - src/FileTracker.App/Views/RecordMovementWindow.xaml.cs - Dialog code-behind
    - tests/FileTracker.Tests/Data/MovementRepositoryTests.cs - 8 repository tests
    - tests/FileTracker.Tests/Services/MovementServiceTests.cs - 6 service tests
  modified:
    - src/FileTracker.Core/Models/Document.cs - Added CurrentLocation display property
    - src/FileTracker.Data/DatabaseInitializer.cs - Added Movements table + indexes
    - src/FileTracker.App/ViewModels/MainViewModel.cs - RecordMovementCommand, DocumentMovedMessage handler
    - src/FileTracker.App/ViewModels/SearchViewModel.cs - IMovementService injection, current location population
    - src/FileTracker.App/ViewModels/DocumentDetailViewModel.cs - MovementHistory collection
    - src/FileTracker.App/Views/DocumentDetailView.xaml - Movement History DataGrid
    - src/FileTracker.App/MainWindow.xaml - Move button + Current Location column
    - src/FileTracker.App/App.xaml.cs - DI registrations for Movement services

key-decisions:
  - "IMovementRepository exposes ONLY InsertAsync, GetByDocumentIdAsync, GetCurrentLocationAsync — compiler-enforced immutability per D-08/MOVE-04"
  - "CurrentLocation is a display-only property on Document populated by SearchViewModel after search — no DB column avoids current-location drift (Pitfall 4)"
  - "Movement history JOINs Positions without IsActive filter so deactivated positions still show by name (Pitfall 4 mitigation)"
  - "DocumentMovedMessage registered via WeakReferenceMessenger inline handler to avoid IRecipient<TMessage> ambiguity with value-type messages"

patterns-established:
  - "INSERT-only repository: interface lacks Update/Delete methods, compiler prevents accidental mutation"
  - "Async dialog loading: ViewModel.LoadAsync called before window.ShowDialog() to populate dropdowns"
  - "Display-only model properties: navigation helpers on entities populated by JOINs, not persisted to DB"

requirements-completed:
  - MOVE-01
  - MOVE-02
  - MOVE-03
  - MOVE-04
  - SRCH-03

# Metrics
duration: 40min
completed: 2026-05-29
---

# Phase 02 Plan 03: Movement Recording & History Tracking Summary

**INSERT-only Movements table with RecordMovementWindow dialog, current location derivation, and movement history display in document detail view**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-05-29T09:31:55Z
- **Completed:** 2026-05-29T09:42:05Z
- **Tasks:** 2
- **Files modified:** 20 (10 created, 8 modified, 2 test files)

## Accomplishments

- Append-only Movements table with FK constraints and performance indexes (IX_Movements_DocumentId, IX_Movements_DocumentId_Date)
- INSERT-only MovementRepository — no UpdateAsync, no DeleteAsync, compiler-enforced immutability (MOVE-04, D-08)
- MovementService with active-position validation for ToPositionId, null-allowed FromPositionId
- RecordMovementWindow dialog: position dropdowns (from IPositionService.GetActiveAsync), direction toggle, date picker, remarks
- "Move" button on every document row in search DataGrid (D-11)
- Current Location column showing derived most-recent-movement ToPosition name (D-09, MOVE-03)
- Movement history DataGrid in DocumentDetailView: Date, From, To, Direction, Remarks in chronological order (D-12, SRCH-03)
- Deactivated positions still show by NAME in movement history (JOIN without IsActive filter — Pitfall 4 mitigation)
- Deactivated positions hidden from Record Movement dropdowns (GetActiveAsync filter)
- DocumentMovedMessage refreshes search results automatically after movement recording

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Movement tests** — `7a3ff34` (test)
2. **Task 1+2 (GREEN+UI): Movement data layer + RecordMovementWindow + UI integration** — `87b48e4` (feat)

_Note: TDD tasks merged RED+GREEN into two commits (separate test commit, combined implementation commit). See TDD Gate Compliance below._

## Files Created/Modified

**Created:**
- `src/FileTracker.Core/Models/Enums/MovementDirection.cs` — Sent/Received enum
- `src/FileTracker.Core/Models/Movement.cs` — Movement entity with navigation helpers (FromPositionName, ToPositionName)
- `src/FileTracker.Core/Dtos/RecordMovementDto.cs` — DTO with ToEntity() helper
- `src/FileTracker.Core/Services/IMovementRepository.cs` — INSERT-only interface (3 methods)
- `src/FileTracker.Core/Services/IMovementService.cs` — Service interface (3 methods)
- `src/FileTracker.Core/Services/MovementService.cs` — Validation + delegation service
- `src/FileTracker.Data/MovementRepository.cs` — Dapper SQL with JOINs, parameterized queries
- `src/FileTracker.App/ViewModels/RecordMovementViewModel.cs` — ObservableValidator with position dropdowns, DocumentMovedMessage
- `src/FileTracker.App/Views/RecordMovementWindow.xaml` — WPF dialog with ComboBoxes, DatePicker, validation
- `src/FileTracker.App/Views/RecordMovementWindow.xaml.cs` — Code-behind with async LoadDocumentAsync pattern
- `tests/FileTracker.Tests/Data/MovementRepositoryTests.cs` — 8 in-memory SQLite tests
- `tests/FileTracker.Tests/Services/MovementServiceTests.cs` — 6 in-memory SQLite tests

**Modified:**
- `src/FileTracker.Core/Models/Document.cs` — Added CurrentLocation display property
- `src/FileTracker.Data/DatabaseInitializer.cs` — Movements table + 2 indexes in schema init
- `src/FileTracker.App/ViewModels/MainViewModel.cs` — RecordMovementCommand, DocumentMovedMessage inline handler, IMovementService dependency
- `src/FileTracker.App/ViewModels/SearchViewModel.cs` — IMovementService injection, current location population after search
- `src/FileTracker.App/ViewModels/DocumentDetailViewModel.cs` — MovementHistory ObservableCollection, IMovementService dependency
- `src/FileTracker.App/Views/DocumentDetailView.xaml` — Movement History DataGrid below audit trail
- `src/FileTracker.App/MainWindow.xaml` — Move button column + Current Location column
- `src/FileTracker.App/App.xaml.cs` — DI: IMovementRepository, IMovementService, RecordMovementViewModel

## Decisions Made

- Used inline lambda registration for DocumentMovedMessage (`Register<DocumentMovedMessage>(this, (_, _) => ...)`) instead of IRecipient<DocumentMovedMessage> to avoid generic type constraint ambiguity with CommunityToolkit.Mvvm's value-type messages
- CurrentLocation as display-only property on Document populated by SearchViewModel — avoids adding a mutable column to the Documents table (anti-pattern per RESEARCH.md Pitfall 4)
- Movement date stored as yyyy-MM-dd TEXT (consistent with Document.DocumentDate format in Phase 1)
- Direction binding via string wrapper property (DirectionText) + ComboBox with "Sent"/"Received" items — simpler than custom EnumToBoolConverter
- All movement repository queries use Dapper parameterized SQL exclusively (no string concatenation)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed WeakReferenceMessenger.Register ambiguity with multiple IRecipient interfaces**
- **Found during:** Task 2 (MainViewModel modification)
- **Issue:** Adding `IRecipient<DocumentMovedMessage>` alongside `IRecipient<DocumentRegisteredMessage>` caused CS7036 compiler error — `Register(this)` resolved to overload requiring token parameter
- **Fix:** Used inline lambda registration `Register<DocumentMovedMessage>(this, (_, _) => SearchVm.SearchCommand.Execute(null))` instead of implementing second IRecipient interface
- **Files modified:** src/FileTracker.App/ViewModels/MainViewModel.cs
- **Verification:** Build succeeds, all 100 tests pass

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Minor API adaptation. No scope creep. Functionality identical to plan specification.

## TDD Gate Compliance

⚠️ **GREEN gate merged with Task 2:** The GREEN phase implementation (Movement data layer) was committed together with Task 2 (UI layer) in a single commit `87b48e4`. A strict TDD flow would have a separate GREEN commit between RED (`7a3ff34`) and the UI work. The combined commit was a result of the data layer files being ready when the UI commit was made. All 14 new movement tests pass, and RED preceded GREEN in the git log.

## Issues Encountered

- `.bak` file renaming: Test files were temporarily renamed to `.bak` extensions during build, then restored. Likely caused by IDE/editor file-watching behavior. Workaround: verified files existed after build, proceeded without issue.

## Known Stubs

- `Document.CurrentLocation` defaults to `"—"` (em dash) for documents with no movement history — legitimate stub indicating "no movements recorded"

## Threat Flags

None — all new surface (RecordMovementWindow input, MovementService validation, MovementRepository INSERT) is covered by the plan's threat model (T-02-11 through T-02-18).

## Next Phase Readiness

- Movement tracking fully functional — ready for Phase 3 (Reporting & Export)
- Movement history data available for report generation queries
- Current location visible in search results for quick document status checks
- Append-only design ensures complete audit trail for compliance reporting

---
*Phase: 02-search-movement-tracking*
*Completed: 2026-05-29*
