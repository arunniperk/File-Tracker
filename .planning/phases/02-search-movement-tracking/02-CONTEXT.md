# Phase 2: Search & Movement Tracking - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Add document search with pagination, a configurable officer hierarchy, and append-only movement tracking. Staff can find any document, view its full history, record movements through the hierarchy, and see current location at a glance.

</domain>

<decisions>
## Implementation Decisions

### Search
- **D-01:** Search by original file number, tracking ID, subject, sender/recipient, and date range. All fields are optional (AND-combined).
- **D-02:** Results displayed in the existing DataGrid with pagination via SQL LIMIT/OFFSET.
- **D-03:** Search is triggered by a Search button (not live filtering).

### Officer Hierarchy
- **D-04:** Officers stored in a configurable Positions table (name, display order, active flag). Not hardcoded.
- **D-05:** Default positions: Faculty/Department, Assistant Registrar, Deputy Registrar, Assistant Executive Engineer, Executive Engineer, Registrar, Dean Admin, Director.
- **D-06:** Positions can be added, renamed, reordered, and deactivated (soft-delete). Deactivated positions hidden from dropdowns.

### Movement Tracking
- **D-07:** Each movement records: document ID, from-position, to-position, direction (sent/received), date, remarks.
- **D-08:** Movements are append-only and immutable (no edit/delete). Audit trait built into the movement history.
- **D-09:** Current location = most recent movement's to-position.

### UI
- **D-10:** Search bar at top of main window with fields and Search button.
- **D-11:** "Record Movement" button on each document row opens a movement dialog (select position, direction, date, remarks).
- **D-12:** Document detail panel shows full movement history in chronological order.

### Claude's Discretion
- Exact search query construction (parameterized SQL)
- Pagination control design
- Movement dialog exact layout
- Position management UI approach

</decisions>

<canonical_refs>
## Canonical References

### Project Definition
- `.planning/PROJECT.md` — Project context and constraints
- `.planning/REQUIREMENTS.md` — SRCH-01..04 and MOVE-01..05 requirements

### Research
- `.planning/research/STACK.md` — Tech stack decisions
- `.planning/research/ARCHITECTURE.md` — Architecture patterns
- `.planning/research/PITFALLS.md` — Critical pitfalls (hardcoded hierarchy, mutable records)

### Prior Phase
- `.planning/phases/01-foundation-data-model-core-registration/01-CONTEXT.md` — Phase 1 decisions carried forward

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RegisterDocumentViewModel`: MVVM pattern to follow for new ViewModels
- `DocumentService.RegisterAsync`: Same DI/service pattern for movement operations
- `DatabaseInitializer`: CreateTable pattern for new Officers and Movements tables
- MainWindow DataGrid: Extend with search controls and pagination

### Established Patterns
- Dapper parameterized SQL (not sqlite-net-pcl or EF Core)
- CommunityToolkit.Mvvm source generators ([ObservableProperty], [RelayCommand])
- WeakReferenceMessenger for cross-VM communication
- Explicit Save button pattern

### Integration Points
- Documents table (add search queries)
- MainWindow (add search bar and movement button column)
- App.xaml.cs DI registration (add new services)

</code_context>

<specifics>
## Specific Ideas

- Officer hierarchy mirrors IIT Dharwad's actual structure: Faculty → Registrar → Dean Admin → Director
- Search should feel like a government record lookup — clean, predictable, no surprises

</specifics>

<deferred>
## Deferred Ideas

- Department, Priority, Document Type fields → deferred from Phase 1, still not in this phase
- Advanced filters (by department, by status) → later phase

</deferred>

---
*Phase: 02-search-movement-tracking*
*Context gathered: 2026-05-29*
