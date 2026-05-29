# Project Research Summary

**Project:** File Tracker — IIT Dharwad Registrar Office Document Tracking System
**Domain:** Government/Educational Records Management (WPF Desktop + SQLite)
**Researched:** 2026-05-29
**Confidence:** HIGH

## Executive Summary

File Tracker is a **single-user Windows 11 WPF desktop application** that digitizes the IIT Dharwad Registrar Office's paper-based document register. It logs every incoming and outgoing document with full metadata, tracks physical movement through the officer hierarchy (Faculty → AR → DR → Registrar → AEE → EE → Dean Admin → Director), attaches scanned copies, and generates monthly summary reports and exports.

The recommended approach is **MVVM with layered architecture on .NET 10 WPF, backed by SQLite via Dapper, and wired together with the .NET Generic Host DI container**. This is the canonical pattern for line-of-business WPF applications — used by PowerToys, Files, and DevToys. CommunityToolkit.Mvvm source generators eliminate MVVM boilerplate, Dapper provides full SQL control with minimal overhead, and SQLite delivers zero-install, crash-resistant single-file storage that handles 100K+ documents effortlessly.

**The three highest risks are: (1) SQLite database corruption from lack of automated backup** — mitigated by timestamped backup on close plus periodic in-use backups to a different physical drive; **(2) hard-coding the officer hierarchy instead of storing it as configurable data** — prevented by modeling positions in a database table with ordering, never in enums or switch statements; **(3) mutable document records without an immutable audit trail** — prevented by append-only movement logging, soft deletes, and making "current location" a derived property from the most recent movement row. All three must be addressed in the Phase 1 data model — retrofitting is a migration nightmare.

## Key Findings

### Recommended Stack

A lightweight, modern .NET stack optimized for single-user desktop: **.NET 10.0 (LTS) + WPF + SQLite + Dapper + CommunityToolkit.Mvvm 8.4+**. The Windows 11 Fluent theme is built into WPF with the `ThemeMode` property — no third-party theming needed. Dapper over EF Core saves 10-15MB and eliminates migration complexity for a single-user app. MSIX packaging for clean install/uninstall, with self-contained single-file `.exe` as fallback for USB deployment. Testing via xunit.v3 + FluentAssertions + Moq.

**Core technologies:**
- **.NET 10.0 (LTS) / C# 14**: Current LTS release (Nov 2025), 3-year support, all ecosystem packages target it
- **WPF (built-in)**: Native Windows 11 performance, Fluent theme with light/dark/system `ThemeMode`
- **CommunityToolkit.Mvvm 8.4.2**: The undisputed MVVM standard — source generators (`[ObservableProperty]`, `[RelayCommand]`) eliminate hundreds of lines of boilerplate
- **SQLite via Microsoft.Data.Sqlite 10.0**: Zero-install, file-based, WAL mode for concurrent read/write, handles 100K+ documents on a single-user desktop
- **Dapper 2.1+**: 10x lighter than EF Core, full SQL control, production-proven in Bitwarden/Sonarr/Radarr
- **Microsoft.Extensions.Hosting 10.0**: Standard .NET Generic Host with DI, configuration, and logging
- **Serilog 4.2+**: Structured file logging, 2.8B+ downloads, used by PowerToys
- **xunit.v3 + FluentAssertions + Moq**: Modern test stack (xunit v2 is deprecated)

**What NOT to use:** EF Core (overhead for single-user), Prism/Caliburn.Micro (legacy MVVM), SQL Server/LocalDB (installation burden), Electron (150MB+ footprint vs 30MB WPF), WinUI 3 (still maturing), xunit v2 (deprecated).

### Expected Features

**Must have (table stakes — the product is unusable without these):**
1. **Document Registration — Incoming** (form-based: sender, subject, file number, type, priority, date, remarks)
2. **Document Registration — Outgoing** (mirror of incoming: recipient, dispatch method, date)
3. **Auto-generated Unique File Number** (`IITDH/REG/YYYY/NNNN` format, unique, sequential)
4. **Current Status & Location Tracking** (which officer holds the document now)
5. **Document Movement / Routing History** (immutable timestamped log: from-officer → to-officer)
6. **Search and Filter** (by file number, sender, subject, date range, current officer, priority)
7. **Document Type Classification** (Memo, Letter, Application, Report, Circular, etc.)
8. **Date Tracking** (receive date, send date, movement dates, expected action date)
9. **Sender / Source Tracking** (name, designation, department)
10. **Data Persistence & Reliable Storage** (SQLite with transactions, automatic backup on close)

**Should have (differentiators — significant workflow improvement):**
1. Scanned Document Attachment (WIA/TWAIN scanner integration, filesystem storage)
2. Officer Hierarchy & Routing Visualization (configurable position list with ordering)
3. Monthly Summary Reports (one-click: all incoming/outgoing for a date range)
4. Report Export to PDF & Excel (for audits, RTI responses, printed records)
5. Priority/Urgency Flagging (Normal/Urgent/Immediate, color-coded)
6. Dashboard / Home View (at-a-glance counts, recent entries, overdue items)
7. Quick-Add with Keyboard Shortcuts (F5/F6 for new, Ctrl+S save, tab navigation)
8. Sender/Department Auto-Complete (fuzzy matching, learns from prior entries)
9. Pending/Overdue Action Tracking (expected action date, visual red highlight)
10. Print-Ready Document Slip (single-click print of a tracking slip for physical attachment)
11. Remarks with Timestamp (per-movement free-text notes)
12. Configurable File Number Format (admin setting for prefix, year toggle, padding)
13. Data Backup & Restore (one-click backup to any location, restore with confirmation)
14. Bulk Status View / List View (spreadsheet-style DataGrid with sortable columns)

**Defer beyond v1 (anti-features — do NOT build):**
Multi-user login/RBAC, email notifications, barcode/QR scanning, cloud sync, workflow approval chains, full-text OCR on attachments, document versioning, mobile app, ERP integration, advanced analytics/charts, redundant audit logs, auto-archival, template-based document generation. See FEATURES.md for the complete rationale on each.

### Architecture Approach

**MVVM + Layered Architecture with Dependency Injection** via .NET Generic Host. Five layers with strict boundaries: **Views** (XAML windows, data binding only) → **ViewModels** (observable state, commands, service orchestration) → **Services** (business logic, validation, orchestration) → **Repositories** (pure data access, no business logic) → **SQLite / Filesystem**. Cross-ViewModel communication uses `WeakReferenceMessenger` from CommunityToolkit.Mvvm — no direct ViewModel references.

**Major components:**
1. **Views (5 windows)**: MainWindow (shell/dashboard), RegisterDocumentWindow (incoming/outgoing forms), TrackDocumentWindow (movement history grid), SearchWindow (search + filter results), ReportWindow (date range + report grid)
2. **ViewModels**: MainViewModel, RegisterDocumentViewModel, TrackDocumentViewModel, SearchViewModel, ReportViewModel — all using `[ObservableProperty]` and `[RelayCommand]` source generators
3. **Services**: DocumentService (register, validate, search, update), MovementService (transfer, history, hierarchy rules), ReportService (aggregation queries, PDF/Excel generation), ScanStore (filesystem save/load for attachments)
4. **Repositories**: DocumentRepository (CRUD via Dapper/SQLite), with pure data access — no business logic
5. **Database**: SQLite with 3 core tables — Documents, Movements, Officers — plus a Config table for hierarchy and file number format

**Critical architectural rules:** Views never touch Repositories. ViewModels never hold persistence logic. Services enforce business rules (e.g., "no backwards hierarchy moves"). Cross-VM communication via Messenger only. Code-behind files must be under 10 lines. Every table gets `CreatedAt`/`ModifiedAt` timestamps. "Current location" is derived from the most recent Movement row — never a mutable column.

### Critical Pitfalls

1. **SQLite Database Corruption from Missing Backup Strategy** — External factors (disk failure, power loss, filesystem corruption) can destroy the single database file. Prevent by: timestamped backup on application close AND periodically during use (store on different physical drive), `PRAGMA integrity_check` on startup, NEVER use `PRAGMA synchronous=OFF`, never place the database on a network share. Must be designed into Phase 1 — retrofitting is error-prone.

2. **SQLite INSERT Performance Collapse Without Explicit Transactions** — By default, each INSERT is its own transaction with disk sync (~60/sec). Without `BEGIN TRANSACTION...COMMIT` wrapping multi-table saves, a document entry taking 2-3 INSERTs becomes 2-3 full fsync operations. Always wrap multi-table saves in explicit transactions. Use WAL journal mode for better read concurrency.

3. **Hard-Coded Officer Hierarchy Becoming Inaccurate** — If officer positions (Registrar, Dean Admin, Director, AR, DR, AEE, EE) are enums or switch statements, every organizational change requires a code change and redeploy. Store positions as data in a configurable table with ordering (HierarchyLevel 1-8). Model movements by `OfficerPositionId`, never by name string. Phase 1 schema must support this.

4. **No Immutable Audit Trail / Mutable Document Records** — Editing or deleting documents without history is a compliance disaster for government records. Never hard-delete (use soft-delete with `IsDeleted`/`DeletedAt`). Never overwrite movement history (each movement is a new append-only row). "Current location" must be a derived property from the most recent movement. Every table needs `CreatedAt`/`ModifiedAt` timestamps.

5. **UI Thread Blocking with Synchronous Database Operations** — Any synchronous DB call on the WPF UI thread freezes the application. ALL database operations must use `async/await`. Use `AsyncRelayCommand` (not synchronous `RelayCommand`). Show loading indicators for operations >300ms. Process scanner input on background threads with `IProgress<T>`. Never call `.Result` or `.Wait()` anywhere in a ViewModel.

6. **Foreign Keys Disabled by Default in SQLite** — SQLite does not enforce FK constraints unless explicitly enabled. Execute `PRAGMA foreign_keys = ON` on every connection open. Add an integration test that verifies FK enforcement is active (insert orphaned child, expect failure). Phase 1 must configure this.

## Implications for Roadmap

The research across all four files converges on a **4-phase build order** that follows the dependency graph: data layer → core document registration → movement tracking → reporting and polish. This aligns with the build waves in ARCHITECTURE.md, the feature dependency chain in FEATURES.md, and the phase-specific pitfall warnings in PITFALLS.md.

### Phase 1: Foundation — Data Model & Core Registration
**Rationale:** Everything depends on the database schema and the ability to register documents. The schema must be designed to support configurable hierarchy, immutable audit trails, reporting queries, and foreign key enforcement from day one. Most of the 6 critical pitfalls live here — getting Phase 1 wrong cascades failure into every subsequent phase.

**Delivers:** SQLite database with all tables (Documents, Movements, Officers, Config), repository interfaces and implementations, .NET Generic Host setup with DI container, incoming and outgoing document registration forms with auto-generated file numbers, file number format configuration in data layer, data persistence with explicit transactions and WAL mode, backup API integration, `PRAGMA foreign_keys = ON`.

**Addresses features:**
- Table Stake #1: Document Registration — Incoming
- Table Stake #2: Document Registration — Outgoing
- Table Stake #3: Auto-generated Unique File Number
- Table Stake #7: Document Type Classification
- Table Stake #8: Date Tracking
- Table Stake #9: Sender / Source Tracking
- Table Stake #10: Data Persistence & Reliable Storage
- Differentiator #12: Configurable File Number Format (schema support)

**Avoids pitfalls:** #1 (backup strategy), #2 (explicit transactions), #3 (configurable hierarchy in schema), #4 (immutable audit trail design), #6 (foreign keys enabled), #9 (reporting queries validated against schema), #12 (UNIQUE constraint on file numbers), #13 (DateOnly types), #15 (MVVM established from first ViewModel), #16 (application-level validation)

**Research flag:** Skip research-phase — this is standard WPF + SQLite CRUD with well-documented patterns (CommunityToolkit.Mvvm, Generic Host, Dapper).

### Phase 2: Movement Tracking & Status
**Rationale:** With registration working, movement tracking is the next dependency in the chain. The officer hierarchy must be configurable in the database (established in Phase 1) before movement logic can reference it. Movement history is append-only by design. This phase also adds the search/filter capability needed to find documents to move.

**Delivers:** Officer hierarchy CRUD UI (add/rename/reorder positions), Document Movement Service with hierarchy validation rules (no backwards moves), immutable movement history with timestamped log, current status/location as derived property, document search and filter with combined criteria, priority/urgency flagging with color coding, remarks with timestamps per movement entry.

**Addresses features:**
- Table Stake #4: Current Status & Location Tracking
- Table Stake #5: Document Movement / Routing History
- Table Stake #6: Search and Filter
- Differentiator #2: Officer Hierarchy & Routing Visualization (configurable)
- Differentiator #5: Priority / Urgency Flagging
- Differentiator #11: Remarks with Timestamp

**Avoids pitfalls:** #3 (hierarchy as configurable data), #4 (append-only movement log, no mutable current-location), #5 (async commands for all DB operations), #10 (WPF memory leaks from event handlers), #14 (DataGrid virtualization for search results)

**Research flag:** Skip research-phase — movement tracking follows standard append-only log patterns. WeakReferenceMessenger is well-documented in CommunityToolkit.

### Phase 3: Dashboard, Reporting & Export
**Rationale:** With documents being registered and moved, the dashboard and reporting layer consumes all that data. Reports depend on movement history and registration data being complete. Export to PDF/Excel is critical for government audit requirements — not optional. This is where the system delivers operational value beyond basic logging.

**Delivers:** Dashboard/Home View with at-a-glance counts (today's incoming/outgoing, documents by status, urgent items, overdue items), Monthly Summary Reports (filter by date range + direction, table format with counts), Report Export to PDF (formatted, print-friendly) and Excel (.xlsx), Pending/Overdue Action Tracking with visual red highlight, Bulk Status View (DataGrid with sortable columns).

**Addresses features:**
- Differentiator #1: Scanned Document Attachment (WIA/TWAIN — added independently)
- Differentiator #3: Monthly Summary Reports
- Differentiator #4: Report Export — PDF / Excel
- Differentiator #6: Dashboard / Home View
- Differentiator #9: Pending / Overdue Action Tracking
- Differentiator #14: Bulk Status View (List View)

**Avoids pitfalls:** #1 (backup on close refactored for robustness), #5 (async report generation), #11 (PDF/Excel export as first-class feature), #13 (inclusive date range queries for reports), #14 (DataGrid virtualization for bulk view)

**Research flag:** Research-phase recommended — PDF generation library selection (QuestPDF vs iTextSharp vs PdfSharp), Excel export library (ClosedXML vs EPPlus licensing), scanner integration (WIA/TWAIN in .NET, Windows Imaging Component). These have multiple viable options with different tradeoffs.

### Phase 4: Polish, UX & Power User
**Rationale:** The core workflow is complete. This phase adds the UX layers that make the application fast and pleasant to use daily. These are independent leaf features — any can be built in any order once the underlying forms exist. Keyboard shortcuts, auto-complete, and print slips are high-value but low-complexity.

**Delivers:** Sender/Department Auto-Complete with fuzzy matching (learns from prior entries), Quick-Add Keyboard Shortcuts (F5 incoming, F6 outgoing, Ctrl+S save, tab navigation), Print-Ready Document Slip (single-click formatted print), Data Backup & Restore UI (file dialogs with confirmation), Error handling and user feedback polish (validation toasts, loading indicators), Configurable File Number Format UI.

**Addresses features:**
- Differentiator #7: Quick-Add with Keyboard Shortcuts
- Differentiator #8: Sender / Department Auto-Complete
- Differentiator #10: Print-Ready Document Slip
- Differentiator #12: Configurable File Number Format (UI)
- Differentiator #13: Data Backup & Restore

**Avoids pitfalls:** #1 (backup UI finalization), #5 (loading indicators on all remaining operations), #10 (final memory leak audit with 8-hour session test)

**Research flag:** Skip research-phase — keyboard shortcuts, auto-complete, and print formatting are standard WPF patterns.

### Phase Ordering Rationale

- **Data layer must come first** because everything depends on it. The schema must be correct before any feature touches it.
- **Registration before movement** because you can't track movement of documents that don't exist in the system. Search is a prerequisite for movement (you need to find a document to move it).
- **Movement before reporting** because reports aggregate movement data. Dashboard needs both registration and movement data to show meaningful counts.
- **Polish last** because auto-complete, keyboard shortcuts, and print slips are UX layers applied on top of working forms. They are independent leaf features that can be added in any order.
- **Scanned attachments** are independently addable (only depend on registration existing) and can be slotted into Phase 3 alongside reporting.
- **This ordering matches the dependency graph in FEATURES.md, the build waves in ARCHITECTURE.md, and the phase-pitfall mapping in PITFALLS.md** — the research is internally consistent.

### Research Flags

**Phases needing deeper research during planning (`/gsd-plan-phase --research-phase`):**
- **Phase 3 (Reporting & Export):** PDF generation library selection (QuestPDF vs PdfSharp vs iTextSharp — licensing, format fidelity, table support), Excel export (ClosedXML vs EPPlus — EPPlus v5+ requires commercial license for some uses), scanner integration (WIA vs TWAIN in .NET — WIA is simpler but TWAIN has broader scanner support, Windows Imaging Component for image processing). These are library-selection decisions with licensing implications.

**Phases with standard patterns (skip research):**
- **Phase 1 (Foundation):** WPF + SQLite CRUD with CommunityToolkit.Mvvm and Generic Host is extensively documented by Microsoft Learn and the CommunityToolkit team. No unknowns.
- **Phase 2 (Movement Tracking):** Append-only logging, WeakReferenceMessenger, and hierarchy validation are standard enterprise patterns with thorough documentation.
- **Phase 4 (Polish/UX):** Keyboard shortcuts, auto-complete, and print formatting follow documented WPF conventions. No novel patterns needed.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified against Microsoft Learn official docs, Context7 verified packages, production usage (PowerToys, Bitwarden, Files). All recommended packages have LTS or stable releases. |
| Features | MEDIUM-HIGH | Table stakes features confirmed against ISO 15489 records management lifecycle and PROJECT.md requirements. Differentiators derived from specific IIT Dharwad hierarchy requirements. Anti-features explicitly documented in PROJECT.md "Out of Scope." Some secondary sources (Indian eOffice conventions) at MEDIUM confidence — not verified against official eOffice docs. |
| Architecture | HIGH | Pattern verified against Microsoft Learn official docs (Layered Architecture, MVVM, DI in .NET, CommunityToolkit.Mvvm). Data models reflect the IIT Dharwad hierarchy. Build wave ordering follows standard dependency inversion. |
| Pitfalls | HIGH | Top pitfalls verified against SQLite official docs (backup API, how-to-corrupt, FAQ, transactions, foreign keys) — all current as of 2024-2026. EF Core and WPF pitfalls verified against Microsoft Learn official docs. Domain-specific pitfalls (hierarchy, audit trail) validated against government records management conventions. |

**Overall confidence: HIGH**

The research across all four dimensions is well-sourced and internally consistent. The stack, architecture, and pitfalls research draws primarily from official documentation (Microsoft Learn, SQLite.org) at HIGH confidence. The features research is at MEDIUM-HIGH due to some Indian government domain conventions being inferred rather than directly verified — but the core requirements come from PROJECT.md stakeholder input.

### Gaps to Address

- **Indian government file numbering conventions:** The `IITDH/REG/YYYY/NNNN` format is inferred from common patterns. The actual IIT Dharwad format should be validated with the Registrar office during Phase 1 planning. The configurable format (Differentiator #12) mitigates this — if the format is wrong, it can be changed.
- **Scanner hardware specifics:** The WIA/TWAIN library selection depends on the actual scanner model available at the Registrar office. This should be verified before Phase 3 implementation. WIA is sufficient for most modern scanners; TWAIN provides broader compatibility with older hardware.
- **Officer hierarchy completeness:** The 8-level hierarchy (Faculty → AR → DR → Registrar → AEE → EE → Dean Admin → Director) is documented in PROJECT.md from the stakeholder interview. If there are additional intermediate positions or parallel reporting structures, the configurable hierarchy table accommodates them — but it should be confirmed during Phase 1.
- **PDF/Excel library licensing:** ClosedXML is MIT-licensed and suitable. EPPlus v5+ switched to a commercial license for commercial use — QuestPDF or PdfSharp are safer alternatives for PDF generation. This needs explicit verification during Phase 3 planning to avoid licensing surprises.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: Common Web Application Architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — Layered architecture patterns, dependency inversion
- [Microsoft Learn: .NET Generic Host in WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder) — DI, configuration, logging for WPF
- [Microsoft Learn: CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — MVVM source generators, ObservableProperty, RelayCommand
- [Microsoft Learn: Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) — DI container patterns
- [Microsoft Learn: WPF Data Binding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/) — Data binding, validation, templates
- [Microsoft Learn: EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/) — Efficient updating, performance diagnosis
- [SQLite Official Docs: Appropriate Uses](https://www.sqlite.org/whentouse.html) — When SQLite is and isn't appropriate
- [SQLite Official Docs: How To Corrupt](https://www.sqlite.org/howtocorrupt.html) — Database corruption vectors and prevention
- [SQLite Official Docs: FAQ](https://www.sqlite.org/faq.html) — Transactions, foreign keys, type affinity, BLOB usage
- [SQLite Official Docs: Backup API](https://www.sqlite.org/backup.html) — Online backup API for live databases
- [Context7: sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) — Async API for SQLite in .NET
- [Wikipedia: Records Management (ISO 15489)](https://en.wikipedia.org/wiki/Records_management) — Records lifecycle: capture, classification, storage, retrieval, circulation, disposition
- [Wikipedia: Document Management System](https://en.wikipedia.org/wiki/Document_management_system) — Canonical DMS component model

### Secondary (MEDIUM confidence)
- PROJECT.md (IIT Dharwad requirements) — Primary project source: validated requirements, hierarchy, constraints
- Domain knowledge: Indian government file tracking conventions (eOffice, CPGRAMS, institutional registrar offices) — Inferred from training data and common patterns; not verified against official eOffice documentation (docs.nic.in inaccessible)
- [.NET MAUI MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm) — MVVM principles applicable to WPF (MAUI-specific but same pattern)

### Tertiary (LOW confidence)
- IJERT/IRJET academic papers on registrar file tracking systems — Paywalled/unfetchable; referenced only for domain pattern confirmation

---

*Research completed: 2026-05-29*
*Ready for roadmap: YES*
