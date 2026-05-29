---
phase: 04-data-safety-management
plan: 02
subsystem: data-safety
tags: [restore, integrity-check, backup-recovery, tdd]
requires: [04-01]
provides: [RestoreFromBackupAsync, CheckDatabaseIntegrityAsync, startup-integrity]
affects: [IBackupService, BackupService, DatabaseInitializer, MainViewModel, MainWindow]
key-decisions:
  - "SqliteException during PRAGMA integrity_check caught and reported as IsOk=false"
  - "Restore validates backup .db via PRAGMA before replacing current data (T-04-04)"
  - "WAL/SHM files cleaned before DB file overwrite to prevent conflicts"
  - "Restore button placed between Backup and Positions in toolbar"
tech-stack:
  added: [System.IO.Compression.ZipFile.ExtractToDirectory, Microsoft.Win32.OpenFileDialog]
  patterns: [TDD, MVVM RelayCommand, PRAGMA integrity_check, SqliteException handling]
key-files:
  created: []
  modified:
    - src/FileTracker.Core/Services/IBackupService.cs
    - src/FileTracker.App/Services/BackupService.cs
    - src/FileTracker.Data/DatabaseInitializer.cs
    - tests/FileTracker.Tests/Services/BackupServiceTests.cs
    - src/FileTracker.App/App.xaml.cs
    - src/FileTracker.App/ViewModels/MainViewModel.cs
    - src/FileTracker.App/MainWindow.xaml
decisions:
  - "SqliteException during PRAGMA integrity_check is caught and treated as corruption (IsOk=false)"
  - "Backup .db is validated via PRAGMA integrity_check before replacing current database (T-04-04 mitigation)"
  - "WAL/SHM journal files are cleaned up before File.Copy to prevent overwrite errors"
  - "Restore does NOT restart the app itself — the caller (MainViewModel) handles restart per D-05"
metrics:
  duration: 20m
  completed: "2026-05-29T11:06:29Z"
---

# Phase 04 Plan 02: Manual Restore & Integrity Check — Summary

**One-liner:** Complete backup/restore loop — manual restore from .zip with destructive confirmation, plus startup database integrity verification via PRAGMA integrity_check.

## Tasks Executed

| # | Name | Type | Commit | Status |
|---|------|------|--------|--------|
| 1 | Add RestoreAsync and CheckIntegrityAsync to BackupService with tests | auto (TDD) | c645434 (RED) → cedb5fb (GREEN) | ✅ Complete |
| 2 | Wire startup integrity check and Restore UI | auto | 0620060 | ✅ Complete |

## Commits

| Hash | Message |
|------|---------|
| c645434 | test(04-02): add failing tests for restore and integrity check (RED) |
| cedb5fb | feat(04-02): implement restore and integrity check (GREEN) |
| 0620060 | feat(04-02): wire startup integrity check and Restore UI |

## Verification

- **Automated:** `dotnet test --filter "BackupService"` — 144/144 tests pass (138 existing + 6 new)
- **Build:** `dotnet build FileTracker.sln` — 0 errors, 36 warnings (all pre-existing)
- **TDD gate sequence:** RED (c645434) → GREEN (cedb5fb) ✅

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] SqliteException during PRAGMA on non-database file unhandled**
- **Found during:** Task 1, test 11 (corrupted DB)
- **Issue:** `CheckDatabaseIntegrityAsync` threw unhandled `SqliteException` ("file is not a database") when running PRAGMA on a non-SQLite file, instead of returning `IsOk=false`
- **Fix:** Added try/catch for `SqliteException` in both `BackupService.CheckDatabaseIntegrityAsync` and `DatabaseInitializer.IntegrityCheckAsync`, returning `IsOk=false` with the exception message
- **Files modified:** `src/FileTracker.App/Services/BackupService.cs`, `src/FileTracker.Data/DatabaseInitializer.cs`
- **Commit:** cedb5fb (folded into GREEN commit)

**2. [Rule 3 - Blocking] FluentAssertions ThrowAsync syntax for async lambdas**
- **Found during:** Task 1, RED phase
- **Issue:** `() => _backupService.RestoreFromBackupAsync(...)` was inferred as non-async, causing `ThrowAsync` to be unavailable on `FunctionAssertions<?>`
- **Fix:** Changed to explicit `Func<Task>` typed lambdas: `Func<Task> act = async () => await _backupService.RestoreFromBackupAsync(...)`
- **Files modified:** `tests/FileTracker.Tests/Services/BackupServiceTests.cs`
- **Commit:** c645434 (folded into RED commit)

### Plan Layout Adjustments

**3. [Execution adjustment] XAML column indices differed from plan assumptions**
- **Found during:** Task 2, MainWindow.xaml editing
- **Issue:** Plan assumed Reports at Column 5, Backup at Column 6, Positions at Column 7 — actual layout had Reports at Column 6, Backup at Column 7, Positions at Column 8
- **Adjustment:** Added Restore at Column 8, shifted Positions to Column 9, page indicator to Column 10, added one `Auto` column definition. Plan's IMPORTANT note explicitly directed checking actual layout.

## Threat Flags

None — all threat model mitigations from the plan were implemented:
- T-04-04: Backup .db validated via PRAGMA integrity_check before replacing current DB ✅
- T-04-05: Only known paths overwritten (`_db.DataSource`, `_attachmentRoot`) ✅
- T-04-06: Zip extraction bounded by local disk (accepted risk) ✅
- T-04-07: PRAGMA integrity_check result used before data operations ✅

## Known Stubs

None — all functionality is fully wired end-to-end.

## Self-Check: PASSED

All 7 source files verified on disk. All 3 commits verified in git history.

