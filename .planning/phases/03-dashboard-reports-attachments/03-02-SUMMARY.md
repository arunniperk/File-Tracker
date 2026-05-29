---
phase: 03-dashboard-reports-attachments
plan: 02
subsystem: attachments
tags: [wpf, sqlite, dapper, mvvm, communitytoolkit, attachment, filesystem]

# Dependency graph
requires:
  - phase: 01-foundation-data-model-core-registration
    provides: "Document model, IDocumentRepository, DI infrastructure"
  - phase: 02-search-movement-tracking
    provides: "Movement tracking, DocumentDetailView layout"
  - phase: 03-dashboard-reports-attachments
    plan: 01
    provides: "TabControl layout, DashboardViewModel, tab switching"
provides:
  - "Attachment model with filesystem metadata + DB pointer pattern"
  - "IAttachmentRepository and IAttachmentService abstractions"
  - "AttachmentService coordinating filesystem operations with SQLite"
  - "UI integration in RegisterDocumentView (pre-creation pending list) and DocumentDetailView (CRUD)"
  - "Timestamp-prefixed filenames for collision prevention"
  - "File validation: extension allowlist, 10MB size limit, path traversal prevention"
affects: [03-reports]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Filesystem metadata + DB pointer pattern for binary storage (no BLOBs)"
    - "Timestamp-prefixed filenames: {yyyyMMdd_HHmmss}_{originalFilename} for uniqueness"
    - "DB-failure cleanup: File.Copy then DB insert, rollback file if insert fails"
    - "Path traversal prevention: Path.GetFileName() strips directory components"
    - "Managed directory validation: Process.Start verifies path is under _attachmentRoot"
    - "Constructor overload: (attachmentRoot: null) defaults to %LocalAppData% for production, testable with explicit path"

key-files:
  created:
    - "src/FileTracker.Core/Models/Attachment.cs - Entity with Id, DocumentId, FileName, StoragePath, FileSize, ContentType, CreatedAt, FileExists"
    - "src/FileTracker.Core/Services/IAttachmentRepository.cs - Repository contract: InsertAsync, GetByDocumentIdAsync, GetByIdAsync, DeleteAsync"
    - "src/FileTracker.Core/Services/IAttachmentService.cs - Service contract: AddAttachmentAsync, GetAttachmentsAsync, RemoveAttachmentAsync, OpenAttachmentAsync"
    - "src/FileTracker.Data/AttachmentRepository.cs - Dapper implementation with parameterized queries"
    - "src/FileTracker.App/Services/AttachmentService.cs - Filesystem + DB coordination with validation and cleanup"
    - "src/FileTracker.App/Converters/FilePathToFileNameConverter.cs - Extracts filename from full path for UI display"
    - "tests/FileTracker.Tests/Services/AttachmentServiceTests.cs - 11 tests covering all attachment operations"
  modified:
    - "src/FileTracker.Data/DatabaseInitializer.cs - Added CREATE TABLE Attachments with index and FK"
    - "src/FileTracker.App/App.xaml.cs - DI registration for IAttachmentRepository and IAttachmentService"
    - "src/FileTracker.App/ViewModels/RegisterDocumentViewModel.cs - Added PendingAttachmentPaths, AddAttachment/RemovePendingAttachment commands"
    - "src/FileTracker.App/Views/RegisterDocumentView.xaml - Added attachment picker and pending file list"
    - "src/FileTracker.App/ViewModels/DocumentDetailViewModel.cs - Added Attachments, AddAttachment/RemoveAttachment/OpenAttachment commands"
    - "src/FileTracker.App/Views/DocumentDetailView.xaml - Added attachment list with Open/Remove buttons"
    - "tests/FileTracker.Tests/ViewModels/RegisterDocumentViewModelTests.cs - Updated for new IAttachmentService parameter"

key-decisions:
  - "Attachments stored on filesystem under %LocalAppData%\\FileTracker\\attachments\\{documentId}\\, not as SQLite BLOBs (per Pitfall 8)"
  - "Filename collision prevention via timestamp prefix ({yyyyMMdd_HHmmss}) rather than GUID"
  - "DB is source of truth: GetAttachmentsAsync returns metadata even if physical file missing (FileExists=false)"
  - "AttachmentService uses constructor parameter for root path (testable), defaults to LocalAppData for production"
  - "NotFoundError: used FileTracker.Core.Exceptions.NotFoundException (with DocumentId property) via alias to resolve ambiguity with FileTracker.Core.Services.NotFoundException"
  - "WPF UI commands (OpenFileDialog, MessageBox) kept in ViewModel layer — no separate UI service abstraction needed for single-user desktop app"

patterns-established:
  - "Filesystem + DB metadata pattern for binary file storage in single-user desktop apps"
  - "Pending list pattern: collect file paths before document creation, process after document ID is known"
  - "IValueConverter for extracting filename from full path (FilePathToFileNameConverter)"
  - "Constructor overload for testable service with optional dependency (attachmentRoot parameter)"

requirements-completed: [ATCH-01, ATCH-02, ATCH-03]

# Metrics
duration: 10min
completed: 2026-05-29
---

# Phase 3 Plan 2: File Attachments — Model, DB, Service, and UI Integration

**Filesystem-based attachment management with DB metadata, supporting PDF/JPG/PNG uploads during registration and from document detail view**

## Performance

- **Duration:** 10 min
- **Started:** 2026-05-29T15:42:00Z
- **Completed:** 2026-05-29T15:52:29Z
- **Tasks:** 2
- **Files modified:** 14 (8 created, 6 modified)

## Accomplishments

- Attachment model with Id, DocumentId, FileName, StoragePath, FileSize, ContentType, CreatedAt, and display-only FileExists property
- IAttachmentRepository with Dapper-based InsertAsync/GetByDocumentIdAsync/GetByIdAsync/DeleteAsync
- AttachmentService coordinating filesystem operations with DB state: copy, validate, insert, cleanup
- 11 automated tests covering: file copy + DB insert, timestamp prefix, nonexistent document rejection, extension validation, size validation, retrieval ordering, empty list, physical deletion, nonexistent attachment rejection, path containment, and missing-file detection
- UI integration: RegisterDocumentView shows pending attachment list with add/remove; DocumentDetailView shows saved attachments with Open/Remove buttons and file size display
- DI registration in App.xaml.cs for IAttachmentRepository and IAttachmentService

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Attachment model, DB table, repository, and service** - `0f2ed3a` (feat)
2. **Task 2: Integrate attachments into RegisterDocumentView and DocumentDetailView** - `fa0b13d` (feat)

## Files Created/Modified
- `src/FileTracker.Core/Models/Attachment.cs` - Entity with display-only FileExists property
- `src/FileTracker.Core/Services/IAttachmentRepository.cs` - Repository contract in FileTracker.Data namespace
- `src/FileTracker.Core/Services/IAttachmentService.cs` - Service contract in FileTracker.Core.Services namespace
- `src/FileTracker.Data/AttachmentRepository.cs` - Dapper implementation with parameterized SQL
- `src/FileTracker.Data/DatabaseInitializer.cs` - CREATE TABLE Attachments with FK and index
- `src/FileTracker.App/Services/AttachmentService.cs` - Filesystem + DB coordination with validation
- `src/FileTracker.App/Converters/FilePathToFileNameConverter.cs` - Extracts filename from full path
- `src/FileTracker.App/App.xaml.cs` - DI registration for attachment services
- `src/FileTracker.App/ViewModels/RegisterDocumentViewModel.cs` - PendingAttachmentPaths + commands
- `src/FileTracker.App/Views/RegisterDocumentView.xaml` - Attachment picker and pending file list
- `src/FileTracker.App/ViewModels/DocumentDetailViewModel.cs` - Attachments collection + Open/Remove/Add commands
- `src/FileTracker.App/Views/DocumentDetailView.xaml` - Attachment list with Open/Remove buttons and file size
- `tests/FileTracker.Tests/Services/AttachmentServiceTests.cs` - 11 automated tests with in-memory SQLite
- `tests/FileTracker.Tests/ViewModels/RegisterDocumentViewModelTests.cs` - Updated for new constructor

## Decisions Made

- Used `FileTracker.Core.Exceptions.NotFoundException` (aliased) to resolve ambiguity with `FileTracker.Core.Services.NotFoundException` — both exist in the codebase with different constructors
- AttachmentService constructor accepts optional `attachmentRoot` string parameter defaulting to null, computing from `%LocalAppData%` when null — enables testability with temp directories
- RegisterDocumentVM tracks pending attachments as full file paths with converter to display-only filenames — avoids creating wrapper class
- DocumentDetailVM confirmation prompt uses WPF `MessageBox.Show` directly in ViewModel — acceptable for single-user desktop app

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CS0104: NotFoundException ambiguous reference**
- **Found during:** Task 1 (AttachmentService implementation)
- **Issue:** Both `FileTracker.Core.Exceptions` and `FileTracker.Core.Services` define `NotFoundException` classes. Using both namespaces caused ambiguous reference errors.
- **Fix:** Used `using NotFoundException = FileTracker.Core.Exceptions.NotFoundException;` alias in AttachmentService.cs and tests.
- **Files modified:** src/FileTracker.App/Services/AttachmentService.cs, tests/FileTracker.Tests/Services/AttachmentServiceTests.cs
- **Verification:** Build succeeds, all 121 tests pass
- **Committed in:** 0f2ed3a (Task 1 commit)

**2. [Rule 3 - Blocking] Missing using System.IO in AttachmentService.cs**
- **Found during:** Task 1 compilation
- **Issue:** `Path`, `File`, `Directory`, `FileInfo` undefined — WPF project doesn't implicitly include System.IO in all contexts
- **Fix:** Added `using System.IO;` to AttachmentService.cs
- **Files modified:** src/FileTracker.App/Services/AttachmentService.cs
- **Committed in:** 0f2ed3a (Task 1 commit)

**3. [Rule 3 - Blocking] Invalid C# syntax `Guid.NewGuid():N` in test file**
- **Found during:** Task 1 test compilation
- **Issue:** `Guid.NewGuid():N` is not valid C# syntax — should be `Guid.NewGuid().ToString("N")`
- **Fix:** Replaced all 6 occurrences with `Guid.NewGuid().ToString("N")`
- **Files modified:** tests/FileTracker.Tests/Services/AttachmentServiceTests.cs
- **Verification:** Build succeeds, tests pass
- **Committed in:** 0f2ed3a (Task 1 commit)

**4. [Rule 3 - Blocking] RegisterDocumentViewModelTests missing IAttachmentService parameter after constructor change**
- **Found during:** Task 2 (test build after ViewModel constructor change)
- **Issue:** Adding IAttachmentService to RegisterDocumentViewModel constructor broke existing tests
- **Fix:** Added `Mock<IAttachmentService>` field and passed to CreateViewModel()
- **Files modified:** tests/FileTracker.Tests/ViewModels/RegisterDocumentViewModelTests.cs
- **Verification:** All 121 tests pass including updated ViewModel tests
- **Committed in:** fa0b13d (Task 2 commit)

**5. [Rule 1 - Bug] Test assertion expected hardcoded filename, actual includes GUID**
- **Found during:** Task 1 test run
- **Issue:** Test asserted `result.FileName.Should().Be("test_pdf")` but source file created with GUID suffix
- **Fix:** Changed assertion to `result.FileName.Should().Be(Path.GetFileName(sourceFile))` to match actual created filename
- **Files modified:** tests/FileTracker.Tests/Services/AttachmentServiceTests.cs
- **Verification:** Test passes
- **Committed in:** 0f2ed3a (Task 1 commit)

---

**Total deviations:** 5 auto-fixed (1 bug, 4 blocking)
**Impact on plan:** All auto-fixes were compilation/runtime corrections. No architectural changes. No scope creep.

## Issues Encountered

- xunit.v3 with Microsoft.Testing.Platform ignores `--filter` parameter on `dotnet test` (MTP0001 warning) — all tests run together but filter is silently ignored. Tests verified by checking total count (121) and pass/fail count.
- CRLF warnings on git add are normal Windows behavior — no action needed.

## Next Phase Readiness

- Attachment infrastructure complete — ready for Phase 3 Plan 3 (Reports with QuestPDF/ClosedXML)
- No new NuGet packages added — QuestPDF and ClosedXML installation deferred to Plan 03-03 per RESEARCH.md
- Attachment storage at `%LocalAppData%\FileTracker\attachments\{documentId}\` ready for report PDF/Excel export storage in same root

## Self-Check: PASSED

- [x] 8 created files verified present
- [x] 2 task commits verified (`0f2ed3a`, `fa0b13d`)
- [x] 1 metadata commit (`844b835`)
- [x] 121 tests pass (110 existing + 11 new)
- [x] Build succeeds with 0 errors, 0 warnings (excluding pre-existing warnings)

---
*Phase: 03-dashboard-reports-attachments*
*Completed: 2026-05-29*
