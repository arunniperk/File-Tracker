---
phase: 02-search-movement-tracking
plan: 02
subsystem: api
tags: [dapper, dynamicparameters, sqlite, wpf, mvvm, communitytoolkit, pagination]

# Dependency graph
requires:
  - phase: 02-search-movement-tracking
    plan: 01
    provides: "DocumentService.GetAllAsync, Document model, in-memory SQLite test pattern"
provides:
  - "Document search with 6 AND-combined optional filters (file number, tracking ID, subject, sender/recipient, date range)"
  - "Dapper DynamicParameters-based parameterized search — zero string concatenation"
  - "Paginated results with page size 20, Prev/Next navigation, total count display"
  - "SearchViewModel with filter properties, Search/Clear/Prev/Next commands"
  - "Search bar UI in MainWindow with text fields, DatePickers, Search/Clear buttons"
affects: [03-reports-exports]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dapper DynamicParameters for dynamic optional-filter SQL construction"
    - "SearchViewModel composed into MainViewModel via DI constructor injection"
    - "CommunityToolkit ObservableProperty + RelayCommand for search UI state"

key-files:
  created:
    - src/FileTracker.Core/Dtos/SearchDocumentDto.cs
    - src/FileTracker.Core/Dtos/SearchResultDto.cs
    - src/FileTracker.App/ViewModels/SearchViewModel.cs
    - tests/FileTracker.Tests/Services/DocumentServiceSearchTests.cs
  modified:
    - src/FileTracker.Core/Services/IDocumentService.cs
    - src/FileTracker.Core/Services/DocumentService.cs
    - src/FileTracker.Core/Services/IDocumentRepository.cs
    - src/FileTracker.Data/DocumentRepository.cs
    - src/FileTracker.App/ViewModels/MainViewModel.cs
    - src/FileTracker.App/MainWindow.xaml
    - src/FileTracker.App/App.xaml.cs

key-decisions:
  - "Search NOT live — triggered by Search button click per D-03"
  - "MainViewModel delegates document list to SearchViewModel.SearchResults"
  - "PageSize clamped to 1..100 in DocumentService for DoS prevention"
  - "Date params passed as yyyy-MM-dd string to match SQLite TEXT storage"
  - "Page/PageSize clamping happens at service layer, not ViewModel"

patterns-established:
  - "Pattern 1: Dynamic Parameterized Search — Dapper DynamicParameters with List<string> conditions, only non-empty filters add WHERE clauses"
  - "Pattern 2: Composed SearchViewModel — injected into MainViewModel, DataGrid binds to SearchVm.SearchResults"

requirements-completed: [SRCH-01, SRCH-02, SRCH-04]

# Metrics
duration: 7min
completed: 2026-05-29
---

# Phase 02 Plan 02: Document Search with Pagination

**Dapper DynamicParameters search with 6 AND-combined filters, paginated WPF DataGrid with Prev/Next navigation**

## Performance

- **Duration:** 7 min
- **Started:** 2026-05-29T09:30:57Z
- **Completed:** 2026-05-29T09:37:36Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Document search by any combination of 6 optional filters (file number, tracking ID, subject, sender/recipient, from date, to date) — all AND-combined
- Paginated results with page size 20, Prev/Next navigation, total count display in page indicator
- Search bar UI at top of MainWindow with text fields, DatePickers, Search and Clear buttons
- View/Edit buttons in search results still open DocumentDetailWindow/load for editing
- 13 search tests covering all filter types, AND combination, date range inclusivity, pagination, soft-delete exclusion, edge cases

## Task Commits

1. **Task 1 (RED): failing tests** - `cb4a256` (test)
2. **Task 1 (GREEN): implementation** - `14ae014` (feat)
3. **Task 2: SearchViewModel + UI** - `06c8742` (feat)

## Files Created/Modified
- `src/FileTracker.Core/Dtos/SearchDocumentDto.cs` — 8 filter properties + pagination params
- `src/FileTracker.Core/Dtos/SearchResultDto.cs` — Results, TotalCount, Page, PageSize, computed TotalPages
- `src/FileTracker.App/ViewModels/SearchViewModel.cs` — Filter state, pagination, Search/Clear/Prev/Next commands
- `tests/FileTracker.Tests/Services/DocumentServiceSearchTests.cs` — 13 search tests
- `src/FileTracker.Core/Services/IDocumentService.cs` — Added SearchAsync method
- `src/FileTracker.Core/Services/DocumentService.cs` — Added SearchAsync with Page/PageSize clamping
- `src/FileTracker.Core/Services/IDocumentRepository.cs` — Added SearchAsync returning tuple
- `src/FileTracker.Data/DocumentRepository.cs` — SearchAsync with Dapper DynamicParameters
- `src/FileTracker.App/ViewModels/MainViewModel.cs` — Composed SearchViewModel, removed LoadDocumentsAsync
- `src/FileTracker.App/MainWindow.xaml` — Search bar at top, pagination below DataGrid
- `src/FileTracker.App/App.xaml.cs` — Registered SearchViewModel in DI

## Decisions Made
- Search NOT live — triggered by Search button click only (D-03 compliance)
- MainViewModel delegates document list to SearchViewModel.SearchResults instead of owning Documents directly
- PageSize clamped to 1..100 in DocumentService for DoS prevention
- Date parameters passed as yyyy-MM-dd string to match SQLite TEXT storage format
- Page/PageSize clamping happens at service layer, not ViewModel (validation closer to data)
- SenderOrRecipient searches BOTH columns: `(Sender LIKE @x OR Recipient LIKE @x)`

## Deviations from Plan

None — plan executed as designed with TDD cycle (RED → GREEN) for Task 1.

## Issues Encountered
- Pre-existing Plan 02-03 stub files (MovementRepositoryTests.cs, MovementServiceTests.cs) blocked test compilation. Temporarily renamed to `.bak` for test execution, restored after verification. Logged in deferred-items.md — will be resolved by Plan 02-03.
- Accidental commit included untracked Plan 02-03 scaffolding files on first attempt — reset and recommitted with only intended files.

## Known Stubs
None — all search functionality is fully wired. No placeholder data sources or hardcoded values.

## Threat Flags
None — all search input goes through Dapper DynamicParameters (parameterized queries). PageSize/Page clamping prevents DoS. Soft-delete exclusion via base WHERE clause.

## Next Phase Readiness
- Search infrastructure complete — SRCH-01, SRCH-02, SRCH-04 requirements fulfilled
- Plan 02-03 can use SearchViewModel.SearchResults as the document list for the "Record Movement" button
- Position management button (Plan 02-01) preserved at bottom of search bar

---
*Phase: 02-search-movement-tracking*
*Completed: 2026-05-29*
