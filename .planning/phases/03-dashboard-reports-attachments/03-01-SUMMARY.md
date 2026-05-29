---
phase: 03-dashboard-reports-attachments
plan: 01
subsystem: ui
tags: [wpf, dashboard, datagrid, tabcontrol, dapper, sqlite, mvvm, communitytoolkit]

# Dependency graph
requires:
  - phase: 01-foundation-data-model-core-registration
    provides: "Document/Position/Movement models, SQLite schema, Dapper repository pattern, DI infrastructure"
  - phase: 02-search-movement-tracking
    provides: "SearchViewModel with filter properties, DocumentRegisteredMessage, DocumentMovedMessage, MainWindow layout"
provides:
  - "Operational dashboard with three sections: pending by officer, recent documents, overdue alerts"
  - "TabControl layout replacing single-view Grid with Dashboard tab as default"
  - "Click-to-navigate: officer name -> filtered search in Documents tab"
  - "DashboardViewModel with auto-refresh on document register/move events"
  - "IDocumentRepository extended with GetPendingByOfficerAsync, GetRecentAsync, GetOverdueAsync"
affects: [03-reports, 03-attachments]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ROW_NUMBER() OVER (PARTITION BY) pattern for single-query dashboard data with CurrentLocation"
    - "Dashboard ViewModel injects IDocumentRepository directly (no service layer per RESEARCH.md)"
    - "WeakReferenceMessenger for cross-tab navigation (SwitchToDocumentsTabMessage)"
    - "IValueConverter for WPF visibility and color binding (CountToVisibilityConverter, OverdueToColorConverter)"

key-files:
  created:
    - "src/FileTracker.Core/Dtos/OfficerPendingCountDto.cs - DTO for officer/pending-count mapping"
    - "src/FileTracker.App/ViewModels/DashboardViewModel.cs - Dashboard VM with 3 collections and navigation"
    - "src/FileTracker.App/Views/DashboardView.xaml - Dashboard UI with three GroupBox sections"
    - "src/FileTracker.App/Views/DashboardView.xaml.cs - Code-behind"
    - "src/FileTracker.App/Converters/CountToVisibilityConverter.cs - Hides sections when zero items"
    - "src/FileTracker.App/Converters/OverdueToColorConverter.cs - Red foreground for overdue"
    - "tests/FileTracker.Tests/Data/DocumentRepositoryDashboardTests.cs - 7 dashboard tests"
  modified:
    - "src/FileTracker.Core/Services/IDocumentRepository.cs - Added 3 dashboard query methods"
    - "src/FileTracker.Data/DocumentRepository.cs - Dapper implementations with single-JOIN SQL"
    - "src/FileTracker.App/ViewModels/MainViewModel.cs - DashboardVm property, tab switching"
    - "src/FileTracker.App/MainWindow.xaml - TabControl replacing root Grid"
    - "src/FileTracker.App/App.xaml.cs - Registered DashboardViewModel as Transient"

key-decisions:
  - "Dashboard queries use single-JOIN ROW_NUMBER() OVER (PARTITION BY) pattern to avoid N+1 per Pitfall 4"
  - "All DateTime filtering uses SQLite datetime('now', '-N days') — no C# DateTime math"
  - "DashboardViewModel injects IDocumentRepository directly (no service layer needed per RESEARCH.md architecture map)"
  - "SwitchToDocumentsTabMessage uses lambda callback pattern (not IRecipient) due to CommunityToolkit.Mvvm 8.4.2 overload resolution"
  - "Overdue section uses light-red row background (#FFF5F5) with red header instead of per-cell converters"

patterns-established:
  - "Single-JOIN SQL with ROW_NUMBER() for dashboard data aggregation"
  - "Direct repository injection in ViewModels for read-only queries"
  - "TabControl with TwoWay SelectedIndex binding for cross-tab navigation"
  - "WeakReferenceMessenger for ViewModel-to-ViewModel communication across tabs"

requirements-completed: [DASH-01, DASH-02, DASH-03]

# Metrics
duration: 8min
completed: 2026-05-29
---

# Phase 3 Plan 1: Operational Dashboard with TabControl Layout

**Dashboard showing pending documents per officer, recent registrations, and overdue alerts — all with click-to-navigate integration with existing search**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-29T10:03:45Z
- **Completed:** 2026-05-29T10:11:59Z
- **Tasks:** 2
- **Files modified:** 13 (7 created, 5 modified)

## Accomplishments
- Officer pending counts via single-JOIN SQL with ROW_NUMBER() for latest movement per document
- Recent documents (last 7 days) with CurrentLocation populated in the same query
- Overdue detection for documents stalled at the same officer >7 days
- TabControl layout with Dashboard as default tab (index 0), Documents tab at index 1
- Click-to-navigate from officer name to filtered search in Documents tab
- Auto-refresh on document registration and movement events
- 7 automated tests covering all three dashboard query methods

## Task Commits

Each task was committed atomically:

1. **Task 1: Dashboard DTOs and repository query methods** - `b4849df` (feat)
2. **Task 2: DashboardView, DashboardViewModel, and TabControl layout** - `ef900bb` (feat)

## Files Created/Modified
- `src/FileTracker.Core/Dtos/OfficerPendingCountDto.cs` - DTO for officer name + document count
- `src/FileTracker.Core/Services/IDocumentRepository.cs` - Added GetPendingByOfficerAsync, GetRecentAsync, GetOverdueAsync
- `src/FileTracker.Data/DocumentRepository.cs` - Single-JOIN Dapper implementations
- `src/FileTracker.App/ViewModels/DashboardViewModel.cs` - Dashboard VM with refresh, navigation, auto-refresh
- `src/FileTracker.App/Views/DashboardView.xaml` - Three-section dashboard UI
- `src/FileTracker.App/Views/DashboardView.xaml.cs` - Code-behind
- `src/FileTracker.App/Converters/CountToVisibilityConverter.cs` - Zero-count visibility converter
- `src/FileTracker.App/Converters/OverdueToColorConverter.cs` - Bool-to-red converter
- `src/FileTracker.App/ViewModels/MainViewModel.cs` - DashboardVm property, SelectedTabIndex, tab switching
- `src/FileTracker.App/MainWindow.xaml` - TabControl with Dashboard + Documents tabs
- `src/FileTracker.App/App.xaml.cs` - DashboardViewModel DI registration
- `tests/FileTracker.Tests/Data/DocumentRepositoryDashboardTests.cs` - 7 tests with in-memory SQLite

## Decisions Made
- Used lambda callback for SwitchToDocumentsTabMessage instead of IRecipient to avoid CommunityToolkit.Mvvm 8.4.2 Register(this) overload ambiguity when multiple IRecipient<T> are on the same class
- Overdue rows styled with light-red background (#FFF5F5) rather than per-cell converters (simpler, plan-compatible)
- SQL queries use parameterized datetime('now', @DateFilter) with string parameter for safety

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed CommunityToolkit.Mvvm Register(this) overload ambiguity**
- **Found during:** Task 2 (DashboardViewModel + MainViewModel integration)
- **Issue:** Adding IRecipient<SwitchToDocumentsTabMessage> to MainViewModel caused Register(this) to fail compilation due to multiple IRecipient<ValueChangedMessage<bool>> implementations
- **Fix:** Used lambda callback pattern (matching existing DocumentMovedMessage pattern) instead of IRecipient
- **Files modified:** src/FileTracker.App/ViewModels/MainViewModel.cs
- **Verification:** Build succeeds, all 107 tests pass
- **Committed in:** ef900bb (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Minimal — changed message registration pattern only, no functional difference.

## Issues Encountered
- Duplicate `using FileTracker.Core.Dtos` warning resolved during cleanup
- CS0105 duplicate using warning in DocumentRepository.cs resolved

## Next Phase Readiness
- Dashboard foundation complete — reports and attachments can now reference dashboard data
- No new NuGet packages added — all existing dependencies sufficient
- Existing search/register/edit/move functionality preserved unchanged in Documents tab

---
*Phase: 03-dashboard-reports-attachments*
*Completed: 2026-05-29*
