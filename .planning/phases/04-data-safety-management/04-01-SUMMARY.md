---
phase: 04-data-safety-management
plan: 01
subsystem: data-safety
tags: [sqlite, backup, zip, system.io.compression, folderbrowserdialog]

# Dependency graph
requires:
  - phase: 03-dashboard-reports-attachments
    provides: "AttachmentService pattern, DI registration conventions, SqliteConnection singleton"
provides:
  - "IBackupService contract with CreateBackupAsync"
  - "BackupService implementation using SqliteConnection.BackupDatabase()"
  - "Timestamped .zip backup containing SQLite DB and attachments directory"
  - "Backup button in MainWindow Documents tab with folder picker UI"
affects:
  - 04-data-safety-management (Plan 02 — restore)
  - 04-data-safety-management (Plan 03 — auto-backup)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SqliteConnection.BackupDatabase() for safe online backup (not File.Copy)"
    - "System.IO.Compression.ZipFile.CreateFromDirectory for archive creation"
    - "Connection pooling disabled on backup destination to ensure file handle release"
    - "Optional attachmentRoot constructor parameter for DI service testability"

key-files:
  created:
    - "src/FileTracker.Core/Services/IBackupService.cs"
    - "src/FileTracker.Core/Models/IntegrityCheckResult.cs"
    - "src/FileTracker.App/Services/BackupService.cs"
    - "tests/FileTracker.Tests/Services/BackupServiceTests.cs"
  modified:
    - "src/FileTracker.App/App.xaml.cs"
    - "src/FileTracker.App/FileTracker.App.csproj"
    - "src/FileTracker.App/ViewModels/MainViewModel.cs"
    - "src/FileTracker.App/MainWindow.xaml"

key-decisions:
  - "Disable SQLite connection pooling on backup destination to ensure file handle release before ZipFile access"
  - "Add optional attachmentRoot constructor parameter for testability (mirrors AttachmentService pattern)"
  - "Use Using Remove for System.Windows.Forms/System.Drawing implicit usings to avoid WPF namespace collision while keeping WinForms framework reference"
  - "Backup filename pattern: FileTracker_Backup_YYYY-MM-DD_HHmmss.zip per D-03"

patterns-established:
  - "TDD RED/GREEN: failing stub tests committed before implementation"
  - "Service testability: optional path parameters enable temp-directory isolation in tests"
  - "Backup service: sql backup via BackupDatabase() + attachments via ZipFile"

requirements-completed: [DATA-01]

# Metrics
duration: 25min
completed: 2026-05-29
---

# Phase 4 Plan 1: Manual Backup Summary

**SQLite safe backup via BackupDatabase() API producing timestamped .zip with database and attachments**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-29T16:16:00Z
- **Completed:** 2026-05-29T16:41:00Z
- **Tasks:** 2
- **Files modified:** 8 (4 created, 4 modified)
- **Tests:** 5 new (all pass), 138 total pass

## Accomplishments
- IBackupService contract with CreateBackupAsync returning zip path
- BackupService creates safe SQLite backup via BackupDatabase() with zip packaging
- Backup button in MainWindow Documents tab with FolderBrowserDialog destination picker
- All 5 integration tests pass: zip contents, valid SQLite, filename pattern, error handling, empty attachments

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IBackupService, BackupService, and tests (TDD)** — 2 commits:
   - `4dbdccc` (test): RED phase — failing tests, stub, interface, model, csproj
   - `bfd940a` (feat): GREEN phase — full BackupService implementation, DI registration
2. **Task 2: Wire Backup UI** — `0cdb7c0` (feat): MainViewModel BackupCommand, MainWindow button

**Plan metadata:** (pending final commit)

## Files Created/Modified
- `src/FileTracker.Core/Models/IntegrityCheckResult.cs` — Model for Plan 02 integrity check results
- `src/FileTracker.Core/Services/IBackupService.cs` — Backup service contract
- `src/FileTracker.App/Services/BackupService.cs` — Backup implementation (140 lines)
- `src/FileTracker.App/App.xaml.cs` — DI registration: IBackupService → BackupService
- `src/FileTracker.App/FileTracker.App.csproj` — Added UseWindowsForms with implicit usings removed
- `src/FileTracker.App/ViewModels/MainViewModel.cs` — Injected IBackupService, added BackupAsync command
- `src/FileTracker.App/MainWindow.xaml` — Added Backup button in Documents tab toolbar
- `tests/FileTracker.Tests/Services/BackupServiceTests.cs` — 5 integration tests

## Decisions Made
- **SqliteConnection.BackupDatabase()** used (not File.Copy) per Pitfall 1 — safe online backup API
- **Pooling=false** on backup destination connection — prevents file handle lock issues with subsequent ZipFile access
- **Optional attachmentRoot** constructor parameter for testability — mirrors existing AttachmentService pattern
- **Using Remove approach** for WinForms implicit usings — avoids 8+ WPF namespace collisions while keeping FolderBrowserDialog available

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] UseWindowsForms caused WPF/WinForms namespace ambiguity**
- **Found during:** Task 1 (build after adding UseWindowsForms)
- **Issue:** Adding `<UseWindowsForms>true</UseWindowsForms>` caused CS0104 ambiguous reference errors across 8+ files (Application, UserControl, MessageBox, Brushes, OpenFileDialog, SaveFileDialog)
- **Fix:** Added `<Using Remove="System.Windows.Forms" />` and `<Using Remove="System.Drawing" />` to csproj to keep the framework reference without implicit global usings. Explicit `using System.Windows.Forms;` added only in MainViewModel.cs where FolderBrowserDialog is used.
- **Files modified:** FileTracker.App.csproj, MainViewModel.cs
- **Committed in:** 4dbdccc (RED phase)

**2. [Rule 1 - Bug] Backup destination connection held file lock preventing ZipFile access**
- **Found during:** Task 1 (GREEN test execution)
- **Issue:** `ZipFile.CreateFromDirectory` failed with "file being used by another process" because the backup destination `SqliteConnection` retained a file handle even after `using` block disposal
- **Fix:** Added `Pooling = false` to the backup destination SqliteConnectionStringBuilder. This ensures the connection is not returned to a pool and the file handle is fully released before ZipFile reads the staging directory.
- **Files modified:** BackupService.cs
- **Committed in:** bfd940a (GREEN phase)

**3. [Rule 2 - Missing] BackupService lacked attachmentRoot testability parameter**
- **Found during:** Task 1 (test compilation)
- **Issue:** BackupService stub had only `(SqliteConnection, ILogger)` constructor; tests needed to inject custom attachment root path for isolated temp directories
- **Fix:** Added optional `string? attachmentRoot = null` constructor parameter with default resolution from `%LocalAppData%\FileTracker\attachments`
- **Files modified:** BackupService.cs, BackupServiceTests.cs
- **Committed in:** 4dbdccc (RED phase)

---

**Total deviations:** 3 auto-fixed (1 blocking, 1 bug, 1 missing critical)
**Impact on plan:** All auto-fixes necessary for correctness and testability. No scope creep. Plan's architectural intent fully preserved.

## Issues Encountered
- File lock on Windows with SQLite connection pooling required explicit `Pooling=false` on destination connection
- WPF/WinForms co-existence in .NET 9 requires careful implicit usings management — `Using Remove` is the least invasive fix

## Known Stubs

None — all functionality is fully implemented. BackupService, UI button, and all 5 tests are complete.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundary changes. Backup writes to local disk only per existing trust model.

## User Setup Required

None — no external service configuration required. The Backup button uses built-in .NET APIs (SqliteConnection.BackupDatabase, ZipFile, FolderBrowserDialog).

## Next Phase Readiness
- Backup infrastructure complete — ready for Plan 02 (Restore from backup)
- `IntegrityCheckResult` model created and waiting for `CheckDatabaseIntegrityAsync` implementation
- `IBackupService` interface stable for Plan 02/03 extensions

---
*Phase: 04-data-safety-management*
*Completed: 2026-05-29*
