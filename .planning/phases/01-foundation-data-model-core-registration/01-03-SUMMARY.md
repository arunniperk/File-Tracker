---
phase: 01-foundation-data-model-core-registration
plan: 03
subsystem: document-edit-audit
tags: [audit-trail, document-edit, field-level-diff, immutability, detail-panel]
requires:
  - 01-02 (tracking ID + toggle)
provides:
  - REG-04 (edit document metadata)
  - REG-05 (audit trail on edits)
  - D-07 (audit display: Timestamp, Field Changed, Old Value, New Value, newest first)
  - D-08 (detail panel separate from registration form)
  - D-09 (no deletion — append-only audit)
affects:
  - Phase 2 (movement tracking builds on audit pattern)
  - Phase 3 (attachment/report features)
tech-stack:
  added: []
  patterns:
    - "DocumentAudit entity with append-only table (no UPDATE/DELETE methods)"
    - "Field-level diff in DocumentService.UpdateAsync via CheckAndAudit helper"
    - "Single IDbTransaction wrapping document UPDATE + audit INSERTs"
    - "Direction/TrackingId excluded from edit (immutable after creation)"
    - "Initial 'Created' audit entry on document registration"
    - "DocumentDetailView with read-only/editable mode toggle via IsEditMode binding"
    - "Audit DataGrid bound to ObservableCollection<DocumentAudit>, IsReadOnly=true"
    - "DocumentDetailWindow opened from MainWindow via RelayCommand on DataGrid row buttons"
    - "RegisterDocumentView dual-mode: create new / edit existing via LoadForEdit()"
    - "Incoming/Outgoing toggle disabled in edit mode (Direction immutable)"
key-files:
  created:
    - src/FileTracker.Core/Models/DocumentAudit.cs
    - src/FileTracker.Core/Services/NotFoundException.cs
    - src/FileTracker.App/ViewModels/DocumentDetailViewModel.cs
    - src/FileTracker.App/Views/DocumentDetailView.xaml
    - src/FileTracker.App/Views/DocumentDetailView.xaml.cs
    - src/FileTracker.App/Views/DocumentDetailWindow.xaml
    - src/FileTracker.App/Views/DocumentDetailWindow.xaml.cs
    - tests/FileTracker.Tests/Data/DocumentRepositoryTests.cs
  modified:
    - src/FileTracker.Core/Services/IDocumentService.cs (added UpdateAsync)
    - src/FileTracker.Core/Services/DocumentService.cs (UpdateAsync + RegisterAsync audit)
    - src/FileTracker.Core/Services/IDocumentRepository.cs (added UpdateAsync, InsertAuditEntryAsync, GetAuditEntriesAsync)
    - src/FileTracker.Data/DocumentRepository.cs (implemented new methods)
    - src/FileTracker.Data/DatabaseInitializer.cs (added DocumentAudit table)
    - src/FileTracker.App/ViewModels/MainViewModel.cs (document selection + navigation commands)
    - src/FileTracker.App/ViewModels/RegisterDocumentViewModel.cs (edit mode + LoadForEdit)
    - src/FileTracker.App/Views/RegisterDocumentView.xaml (mode indicator, toggle disable, button text)
    - src/FileTracker.App/MainWindow.xaml (View/Edit buttons per DataGrid row)
    - src/FileTracker.App/App.xaml.cs (DocumentDetailViewModel registration + Services property)
    - tests/FileTracker.Tests/Services/DocumentServiceTests.cs (26 new audit/edit tests)
key-decisions:
  - "Direction and TrackingId are never updated during document edit — excluded from CheckAndAudit and UPDATE SQL"
  - "DocumentAudit table has no UPDATE/DELETE repository methods — append-only per D-09"
  - "Audit trail query orders by ChangedAt DESC (newest first) per D-07"
  - "RegisterAsync inserts initial 'Created' audit entry in the same transaction as document insert"
  - "Document detail opens in separate window (DocumentDetailWindow) per D-08"
  - "Registration form re-used for editing via LoadForEdit() pre-population and IsEditMode flag"
metrics:
  duration: 12 min
  completed_date: 2026-05-29
  tasks: 2
  files: 21
  commits: 3
  tests_added: 26
  tests_total: 46
---

# Phase 01 Plan 03: Document Edit with Immutable Field-Level Audit Trail — Summary

**One-liner:** Added document detail view with metadata editing and an immutable field-level audit trail that records every change as append-only entries displayed newest-first.

## Plan Execution

Executed in 2 tasks across 3 commits:

| Task | Name | Commit | Key Output |
|------|------|--------|-----------|
| 1 | Document edit pipeline with field-level diff and audit trail storage | `b4cb1a3` (RED) → `9fa0fb2` (GREEN) | DocumentAudit entity, UpdateAsync with CheckAndAudit, atomic transaction, initial "Created" audit entry |
| 2 | Document detail panel with edit controls and audit trail display | `46c6951` | DocumentDetailView/Window, edit mode in registration form, View/Edit buttons on DataGrid |

## RED/GREEN TDD Gate Compliance

Task 1 followed the full TDD cycle:
1. **RED** (`b4cb1a3`): 44 compilation errors — `UpdateAsync`, `InsertAuditEntryAsync`, `GetAuditEntriesAsync` did not exist
2. **GREEN** (`9fa0fb2`): All 46 tests passing after implementing repository, service, database changes
3. Tests were written before implementation code was added

## Verification Results

- **Build:** Passed — `dotnet build FileTracker.sln` 0 errors
- **Tests:** Passed — `dotnet test tests/FileTracker.Tests` — 46/46 passed, 0 failed, 0 skipped
- **Existing tests preserved:** All Plan 01-01 and 01-02 tests still pass (including REG-01, REG-02, REG-03)

## Key Architecture Decisions

1. **Field-level diff via CheckAndAudit helper:** DocumentService.UpdateAsync fetches the existing record, compares each mutable field using a local `CheckAndAudit(string, string?, string?)` function, and collects `DocumentAudit` entries for changed fields only.

2. **Atomic transaction for editing:** The document UPDATE and all audit INSERTs share a single `IDbTransaction`. If any operation fails, the entire transaction rolls back, preventing partial updates.

3. **Append-only audit table:** The `DocumentAudit` table has only INSERT and SELECT repository methods. There are no UPDATE or DELETE methods — the audit trail is immutable per D-09.

4. **Direction and TrackingId exclusion:** These fields are NOT compared in `CheckAndAudit` and NOT included in the UPDATE SQL. Direction is immutable after document creation per the plan spec.

5. **Initial creation audit:** `RegisterAsync` inserts a `DocumentAudit` row with `FieldName="Created"`, `OldValue=null`, `NewValue="Document registered"` in the same transaction as the document INSERT.

6. **Document detail as separate window:** Per D-08, clicking "View" opens a `DocumentDetailWindow` (separate from the registration form) showing all metadata and the audit trail DataGrid.

7. **Registration form dual-mode:** The `RegisterDocumentView` serves both creation and editing. `LoadForEdit(document)` pre-populates the form, sets `IsEditMode=true`, disables the Direction toggle, and routes `SubmitAsync()` to `UpdateAsync` instead of `RegisterAsync`.

## Deviations from Plan

None — plan executed exactly as written. All must-haves, artifacts, and acceptance criteria were met.

## Known Stubs

None — all functionality is fully implemented and tested.

## Threat Flags

None — all STRIDE threats from the plan's threat model are addressed:
- T-01-11 (Tampering: audit entries modified): DocumentAudit has no UPDATE/DELETE methods, DataGrid is read-only
- T-01-12 (Tampering: partial update): Both operations share single transaction
- T-01-13 (Tampering: Direction changed): Excluded from CheckAndAudit and UPDATE SQL, UI toggle locked
- T-01-14 (Spoofing: wrong document): UI displays tracking ID in edit header — accepted for single-user desktop
- T-01-15 (Tampering: concurrent edit): Single-user app — accepted
- T-01-16 (Info disclosure): Audit trail display is a feature per D-07 — accepted

## Requirements Fulfilled

| Requirement | Description | Status |
|-------------|-------------|--------|
| REG-04 | Edit document metadata | Complete |
| REG-05 | Audit trail on edits | Complete |
| D-07 | Edit history: Timestamp, Field Changed, Old Value, New Value, newest first | Complete |
| D-08 | Detail panel separate from registration form | Complete |
| D-09 | No deletion — append-only audit | Complete |
