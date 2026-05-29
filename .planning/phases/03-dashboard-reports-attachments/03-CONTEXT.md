# Phase 3: Dashboard, Reports & Attachments - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Add operational dashboard with pending/overdue tracking, monthly summary reports with PDF and Excel export, and scanned document attachment support. Staff get at-a-glance visibility and can generate reports for audits.

</domain>

<decisions>
## Implementation Decisions

### Dashboard
- **D-01:** Dashboard is the default view when app opens (replaces or sits alongside document list)
- **D-02:** Shows three sections: documents pending at each officer (with counts), recently registered (last 7 days), overdue documents (pending > 7 days, highlighted red)
- **D-03:** Clicking a pending count or overdue item navigates to filtered search results

### Reports
- **D-04:** Monthly report: select month + year, generates summary of all incoming/outgoing for that period
- **D-05:** Report includes breakdowns by document type, by department, by priority — using existing document fields
- **D-06:** PDF export via QuestPDF (MIT license, pure .NET, no external dependencies)
- **D-07:** Excel export via ClosedXML (MIT license) — raw data export for further processing

### Attachments
- **D-08:** Add attachment button on registration form and edit form — opens file picker (PDF, JPG, PNG)
- **D-09:** Attachments stored as files on disk (%LocalAppData%\FileTracker\attachments\{documentId}\)
- **D-10:** Attachment list shown on document detail view with "Open" button (opens in default viewer)
- **D-11:** Attachments can be removed individually

### Claude's Discretion
- Dashboard exact layout and styling
- Report PDF template design
- Attachment file naming convention
- Error handling for missing files

</decisions>

<canonical_refs>
## Canonical References

### Project Definition
- `.planning/PROJECT.md` — Project context and constraints
- `.planning/REQUIREMENTS.md` — DASH-01..03, RPT-01..04, ATCH-01..03

### Research
- `.planning/research/STACK.md` — Tech stack
- `.planning/research/PITFALLS.md` — Attachment storage (filesystem not BLOBs)

### Prior Phases
- `.planning/phases/01-foundation-data-model-core-registration/01-CONTEXT.md` — Phase 1 decisions
- `.planning/phases/02-search-movement-tracking/02-CONTEXT.md` — Phase 2 decisions (positions, search, movements)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- DocumentService, DocumentRepository: Extend with attachment methods and report queries
- MainWindow: Add dashboard panel or repurpose layout
- SearchViewModel pattern: Follow for report generation UI
- MovementRepository: Follow append-only pattern for attachment tracking

### Integration Points
- MainWindow: Dashboard becomes default or tabbed view
- RegisterDocumentView: Add attachment button
- DocumentDetailView: Add attachment list
- App.xaml.cs DI: Register new services (IAttachmentService, IReportService)

</code_context>

<specifics>
## Specific Ideas

- Dashboard should feel like a Registrar office summary board — counts, pending items, overdue alerts
- Reports should match government audit requirements — clear, printable, with proper headers
- Attachments should be simple — no scanner integration yet, just file picker

</specifics>

<deferred>
## Deferred Ideas

- Scanner integration (WIA/TWAIN) — Phase 3 uses file picker only
- Custom report builder — deferred to v2
- Email reports — out of scope

</deferred>

---
*Phase: 03-dashboard-reports-attachments*
*Context gathered: 2026-05-29*
