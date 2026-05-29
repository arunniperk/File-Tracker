---
phase: 01-foundation-data-model-core-registration
plan: 02
subsystem: document-registration
tags: [tracking-id, validation, toggle, unsaved-changes]
dependency_graph:
  requires: [01-01]
  provides: [REG-02, REG-03]
  affects: [01-03]
tech-stack:
  added:
    - patterns: [ObservableValidator, INotifyDataErrorInfo, UPSERT RETURNING, IDbTransaction]
  patterns:
    - atomic-tracking-id: SQLite ON CONFLICT DO UPDATE RETURNING
    - form-validation: ObservableValidator with [NotifyDataErrorInfo]
    - unsaved-changes: Window.Closing + MessageBox
key-files:
  created:
    - src/FileTracker.App/Converters/BoolToVisibilityConverter.cs
    - src/FileTracker.App/Converters/BoolInvertVisibilityConverter.cs
    - src/FileTracker.App/Converters/BoolInvertConverter.cs
  modified:
    - src/FileTracker.Data/DatabaseInitializer.cs (adds TrackingSequence table)
    - src/FileTracker.Core/Services/IDocumentRepository.cs (adds GetNextSequenceAsync)
    - src/FileTracker.Data/DocumentRepository.cs (UPSERT RETURNING + transaction support)
    - src/FileTracker.Core/Services/DocumentService.cs (tracking ID integration)
    - src/FileTracker.App/ViewModels/RegisterDocumentViewModel.cs (ObservableValidator)
    - src/FileTracker.App/Views/RegisterDocumentView.xaml (Incoming/Outgoing toggle)
    - src/FileTracker.App/ViewModels/MainViewModel.cs (HasUnsavedChanges)
    - src/FileTracker.App/MainWindow.xaml (TrackingId + Direction columns)
    - src/FileTracker.App/MainWindow.xaml.cs (OnClosing handler)
    - src/FileTracker.App/App.xaml (converter registrations)
    - tests/FileTracker.Tests/Services/DocumentServiceTests.cs (tracking ID tests)
    - tests/FileTracker.Tests/ViewModels/RegisterDocumentViewModelTests.cs (new)
    - tests/FileTracker.Tests/FileTracker.Tests.csproj (net9.0-windows + App ref)
decisions:
  - "Tracking ID uses D4/YYYY format via UPSERT RETURNING for atomic yearly-reset"
  - "Tracking ID and document INSERT share same IDbTransaction for atomicity"
  - "ObservableValidator with [NotifyDataErrorInfo] for WPF-native validation"
  - "Unsaved changes detected via _isClearing guard to prevent false positives during ClearForm"
  - "BoolInvertConverter for Outgoing RadioButton (IsIncoming inversion)"
metrics:
  duration: "~8 min"
  completed: "2026-05-29T08:30:00Z"
---

# Phase 01 Plan 02: Tracking IDs, Toggle & Validation Summary

**One-liner:** Atomic yearly-reset tracking IDs (0001/YYYY), Incoming/Outgoing radio toggle with field swapping, ObservableValidator form validation, and unsaved changes dialog — all with TDD.

## Completed Tasks

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Tracking ID generation with atomic yearly-reset UPSERT | `08e38df` (GREEN) | DatabaseInitializer.cs, IDocumentRepository.cs, DocumentRepository.cs, DocumentService.cs |
| 2 | Incoming/Outgoing toggle, ObservableValidator, unsaved changes | `1762263` (GREEN) | RegisterDocumentViewModel.cs, RegisterDocumentView.xaml, MainWindow.xaml.cs, 3 converters |

**TDD Gates:**
- Task 1 RED: `7494d1b` (6 tracking ID tests) → GREEN: `08e38df` (implementation)
- Task 2 RED: `bc8eb41` (10 ViewModel tests) → GREEN: `1762263` (implementation)

## Test Results

```
All 24 tests passing (14 DocumentService + 10 ViewModel)
dotnet test tests/FileTracker.Tests → Passed! 0 Failed, 0 Skipped
```

## Deviations from Plan

None — plan executed exactly as written.

## Key Deliverables

1. **Tracking IDs (REG-03):** `0001/2026` format via `INSERT...ON CONFLICT(Year) DO UPDATE...RETURNING LastNumber`. Atomic with document INSERT in same transaction — rollback prevents sequence waste.

2. **Outgoing Registration (REG-02):** Single form with Incoming/Outgoing RadioButton toggle at top. Label swaps between "Sender" and "Recipient" via `BoolToVisibilityConverter`/`BoolInvertVisibilityConverter`.

3. **Form Validation (D-10):** `ObservableValidator` base class with `[NotifyDataErrorInfo]` and `[Required]` attributes on Subject, FileNumber, SenderOrRecipient, Date. `CanSubmit` gate prevents save when fields empty. WPF binds to `INotifyDataErrorInfo` natively.

4. **Unsaved Changes (D-11):** `HasUnsavedChanges` flag on ViewModel tracked via `OnPropertyChanged` partial methods. `_isClearing` guard prevents false triggers during `ClearForm()`. `MainWindow.OnClosing` shows MessageBox "You have unsaved changes. Discard them?" — Yes closes, No cancels.

5. **Document List:** DataGrid now shows TrackingId and Direction columns alongside existing fields.

## Threat Mitigations

| Threat | Mitigation | Status |
|--------|-----------|--------|
| T-01-06: Tracking ID race condition | SQLite `ON CONFLICT...DO UPDATE...RETURNING` atomic statement | ✅ |
| T-01-07: Tracking ID consumed but doc not saved | Same `IDbTransaction` — rollback restores sequence | ✅ |
| T-01-08: Form submitted with invalid direction | Direction derived from radio state, not user string | ✅ |
| T-01-09: Duplicate original file number | UNIQUE constraint catch → user-friendly error message | ✅ |
| T-01-10: User claims "I didn't mean to discard" | D-11 confirmation dialog | ✅ |

## Self-Check: PASSED

- [x] `dotnet build FileTracker.sln` — 0 errors
- [x] `dotnet test tests/FileTracker.Tests` — 24 passed, 0 failed
- [x] All acceptance criteria verified via grep (ON CONFLICT, RETURNING LastNumber, D4 format)
- [x] All existing Plan 01 tests still pass
