# Phase 1: Foundation — Data Model & Core Registration - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver the SQLite database, MVVM project scaffold, and document registration forms. Registrar staff can register incoming and outgoing documents with original file numbers, auto-generated tracking IDs, and immutable edit audit trails. Data persists across restarts.

</domain>

<decisions>
## Implementation Decisions

### File Numbers
- **D-01:** User manually enters the original file number shown on the physical document (for both incoming and outgoing).
- **D-02:** System auto-generates an internal tracking ID in `Sl.No/YYYY` format (e.g., `0001/2026`), resetting yearly. Both the original file number and tracking ID are stored and searchable.
- **D-03:** File number uniqueness is enforced on the original file number field. The auto-generated tracking ID is guaranteed unique.

### UI Layout
- **D-04:** Single registration form with an Incoming/Outgoing toggle that swaps the first field (Sender ↔ Recipient).
- **D-05:** MVP fields for Phase 1: Sender (incoming) / Recipient (outgoing), Subject, Date, File Number (manual entry), Remarks. Department, Priority, and Document Type deferred to later phases.
- **D-06:** Form has a clean, single-column layout. The toggle is a prominent radio button or segmented control at the top of the form.

### Audit Trail
- **D-07:** Edit history displayed as a simple log table: Timestamp, Field Changed, Old Value, New Value. Chronological order, newest first.
- **D-08:** Audit trail is viewed from the document detail panel (separate from the registration form).
- **D-09:** Deletion is NOT supported — edits to documents are tracked, records are never removed.

### Save Behavior
- **D-10:** Explicit Save button — user fills form and clicks Save to persist. Form is cleared after successful save.
- **D-11:** If user attempts to close or navigate away with unsaved changes, show a warning dialog: "You have unsaved changes. Discard them?"

### Claude's Discretion
- Database schema design (exact table structure, column types, indexes)
- WPF MVVM project structure and file organization
- Exact form styling, spacing, and typography
- Error handling and validation patterns
- DI container setup and service registration
- SQLite connection string and WAL mode configuration

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Definition
- `.planning/PROJECT.md` — Project context, constraints, and key decisions for IIT Dharwad File Tracker
- `.planning/REQUIREMENTS.md` — Full v1 requirements, REG-01 through REG-05 defined in Document Registration category

### Research
- `.planning/research/STACK.md` — Technology stack: .NET 10 WPF, SQLite, Dapper, CommunityToolkit.Mvvm, Serilog
- `.planning/research/ARCHITECTURE.md` — MVVM + Layered Architecture with DI, 4-layer structure, build order
- `.planning/research/PITFALLS.md` §Phases 1-2 — Critical pitfalls: SQLite PRAGMA foreign_keys, transaction batching, soft deletes, UI thread blocking

### Roadmap
- `.planning/ROADMAP.md` §Phase 1 — Success criteria and requirements mapping

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project. All code is new.

### Established Patterns
- CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) for MVVM
- Dapper with raw SQL for data access (no EF Core)
- Microsoft.Extensions.Hosting Generic Host for DI and configuration
- WPF built-in Windows 11 Fluent theme

### Integration Points
- Application entry point: `App.xaml` / `App.xaml.cs` with Generic Host setup
- Database initialization on app startup
- Navigation: single-window app, no multi-page routing needed in Phase 1

</code_context>

<specifics>
## Specific Ideas

- Form should feel like a government office data-entry screen — simple, clear labels, no unnecessary animations
- Tracking ID generation should be reliable and predictable (not random/UUID)
- User mentioned the IIT Dharwad website (iitdh.ac.in) as organizational reference — any branding/styling should defer to institutional norms

</specifics>

<deferred>
## Deferred Ideas

- Department, Priority, and Document Type fields — deferred to Phase 2 (Search & Movement Tracking) when filtering/searching by these fields becomes meaningful
- Configurable file number format (IITDH/REG/YYYY/NNNN) — Phase 1 uses simple Sl.No/YYYY. Custom formats deferred.

</deferred>

---

*Phase: 01-foundation-data-model-core-registration*
*Context gathered: 2026-05-29*
