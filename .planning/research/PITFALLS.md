# Domain Pitfalls

**Domain:** Document/File Tracking Systems (WPF Desktop + SQLite for Government/Educational Offices)  
**Researched:** 2026-05-29  
**Confidence:** HIGH (verified with SQLite official docs, Microsoft Learn, EF Core docs)

---

## Critical Pitfalls

Mistakes that cause data loss, corruption, or complete rewrites.

### Pitfall 1: SQLite Database Corruption from Missing Backup Strategy

**What goes wrong:** The SQLite database file becomes corrupted due to disk failure, power loss during write, or accidental file overwrite. Without a backup, months or years of document registry data is permanently lost. Government offices cannot function without this audit trail.

**Why it happens:** SQLite stores the entire database in a single file. While SQLite itself is crash-resistant, external factors (disk failure, rogue processes overwriting the file, defective USB drives, filesystem corruption) can destroy it. Unlike server databases, there is no built-in replication or automated backup. Per [SQLite's corruption documentation](https://www.sqlite.org/howtocorrupt.html): "SQLite does not corrupt database files without external help" — but the external help is common on single-user desktops.

**Consequences:** Complete loss of all document tracking data. No recovery path unless backups exist. Government/educational offices cannot reconstruct document trails.

**Prevention:**
1. Use SQLite's [backup API](https://www.sqlite.org/backup.html) to create timestamped backup copies on application close and periodically during use
2. Store backups on a different physical drive (not the same disk as the working database)
3. Use `PRAGMA integrity_check` on startup to detect corruption early
4. Never set `PRAGMA synchronous=OFF` — the default FULL setting is required for data safety per SQLite docs
5. Export data to a portable format (PDF/CSV reports) that can be independently stored and printed
6. NEVER place the database file on a network share — SQLite docs explicitly warn: "file locking logic is buggy in many network filesystem implementations... corruption might result"

**Detection:** Database fails to open, `PRAGMA integrity_check` returns errors, application crashes on startup.

**Phase to address:** Phase 1 (Core Data Layer) — backup must be designed into the data layer from day one; retrofitting is error-prone.

---

### Pitfall 2: SQLite INSERT Performance Collapse Without Explicit Transactions

**What goes wrong:** The application becomes unusably slow after a few hundred document entries. Saving each new document takes seconds instead of milliseconds.

**Why it happens:** Per [SQLite FAQ #19](https://www.sqlite.org/faq.html#q19): "By default, each INSERT statement is its own transaction. Transaction speed is limited by disk drive speed... about 60 transactions per second." Without wrapping batch operations in `BEGIN...COMMIT`, each individual save becomes a full transaction with disk sync. For a document entry that saves to 2-3 related tables, that's 2-3 full fsync operations.

**Consequences:** Users perceive the app as broken. Staff may enter data incorrectly if the UI freezes during saves. Can make monthly reporting unusable if reports walk full tables row-by-row.

**Prevention:**
1. Always wrap multi-table document saves in explicit `BEGIN TRANSACTION...COMMIT`
2. For bulk operations (imports, monthly report generation), use single transactions
3. Use EF Core's `SaveChangesAsync()` which automatically batches changes — but verify batching is actually occurring
4. Consider `PRAGMA journal_mode=WAL` for better concurrent read performance (safe for single-user apps too)
5. Do NOT use `PRAGMA synchronous=OFF` to fix this — it masks the symptom while introducing data corruption risk
6. For large datasets in DataGrid: use pagination/virtualization, NOT loading all rows

**Detection:** Save operations take >500ms for a single document entry. UI freezes during saves. Application startup gets progressively slower as database grows.

**Phase to address:** Phase 1 (Core Data Layer) and Phase 2 (Document Entry Forms)

---

### Pitfall 3: Hard-Coded Officer Hierarchy and Document Flow Paths

**What goes wrong:** The workflow hierarchy (Faculty → Registrar → Dean Admin → Director, plus AR, DR, AEE, EE) is hard-coded in the application. When officers change, departments restructure, or new positions are added, the application breaks or becomes inaccurate.

**Why it happens:** Developers model the hierarchy as an enum or a fixed path string rather than a configurable data structure. Government and educational institutions undergo frequent administrative restructuring.

**Consequences:** Application becomes inaccurate or unusable within months. Staff work around the system, defeating its purpose. Requires code changes (recompile, redeploy) for every organizational change.

**Prevention:**
1. Store the officer/department hierarchy as **data in a configuration table**, not as code
2. Allow adding, renaming, and reordering officers/departments through the UI
3. Model the hierarchy as an ordered sequence (1→2→3→4) rather than named stages
4. Document movements should reference `OfficerPositionId`, not a hard-coded string
5. Include an "active/inactive" flag for positions so historical data preserves references to retired positions

**Detection:** Officer names appear in C# enum definitions or `switch` statements. Adding a new officer requires code changes. Search for hard-coded strings like "Registrar", "Dean Admin", "Director".

**Phase to address:** Phase 1 (Data Model / Schema Design) — the database schema must support configurable hierarchy from the start.

---

### Pitfall 4: No Immutable Audit Trail / Mutable Document Records

**What goes wrong:** Document records can be edited or deleted after creation. There is no permanent, immutable record of what was originally entered. In a government/educational context, this is a compliance and accountability disaster.

**Why it happens:** Developers model documents as simple CRUD entities. Deletes are hard deletes. Updates overwrite original values. There is no distinction between "correcting a typo" and "changing who sent a document."

**Consequences:** Audit failure during inspection. Inability to prove document provenance. Staff can alter records after the fact without trace. The system loses all credibility as an official register.

**Prevention:**
1. Never hard-delete document records — use a soft-delete flag (`IsDeleted`, `DeletedAt`, `DeletedBy`)
2. Track document movements in a separate `DocumentMovements` table (not as a `CurrentLocation` column on the document)
3. Never overwrite: each movement is a new row with timestamp, from-officer, to-officer, action, and user
4. Add `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` timestamps on every table
5. Consider an `AuditLog` table for schema-level changes (who changed the officer hierarchy, when)
6. The document's "current location" should be a **computed/derived** property: the `ToOfficerId` of the most recent movement

**Detection:** Database schema shows `UPDATE` and `DELETE` operations on documents without audit columns. No `DocumentMovements` table exists.

**Phase to address:** Phase 1 (Data Model) — immutability must be designed into the schema. Retrofitting after data exists is a migration nightmare.

---

### Pitfall 5: UI Thread Blocking with Synchronous Database Operations

**What goes wrong:** The WPF UI freezes (becomes unresponsive) during database operations, especially when saving documents with scanned attachments or generating monthly reports. Users see "Not Responding" in the title bar.

**Why it happens:** WPF has a single UI thread. Synchronous database calls on this thread block all UI updates. Even SQLite operations that take 200-500ms (scanning BLOB attachment, running a report query) will freeze the UI.

**Consequences:** Poor user experience. Staff may force-close the application, potentially corrupting the database mid-transaction. Users lose trust in the application.

**Prevention:**
1. ALL database operations go through `async/await` with `Task.Run()` or `ConfigureAwait(false)`
2. Use EF Core's `SaveChangesAsync()`, `ToListAsync()`, `FirstOrDefaultAsync()` — never the synchronous variants
3. For WPF commands that trigger DB work: use `AsyncRelayCommand` (from CommunityToolkit.Mvvm), not synchronous `RelayCommand`
4. Show loading indicators during operations longer than 300ms
5. For large file attachment scanning: process on background thread with progress reporting via `IProgress<T>`
6. Use `Dispatcher.InvokeAsync()` only for the final UI update after background work completes

**Detection:** Any call to `.Result`, `.Wait()`, or synchronous EF methods (`SaveChanges()`, `ToList()`) in ViewModel or code-behind. Missing `async` keywords on command handlers.

**Phase to address:** Phase 2 (Document Entry) and Phase 4 (Reporting) — async patterns must be established in Phase 2 and consistently applied.

---

### Pitfall 6: Foreign Keys Disabled by Default in SQLite

**What goes wrong:** SQLite does NOT enforce foreign key constraints unless explicitly enabled. Orphaned child records (document movements without a valid document, attachments pointing to deleted documents) accumulate silently.

**Why it happens:** Per [SQLite FAQ #22](https://www.sqlite.org/faq.html#q22): "Enforcement of foreign key constraints is turned off by default (for backwards compatibility)." This is a well-known SQLite default that surprises developers coming from SQL Server or PostgreSQL.

**Consequences:** Data integrity silently degrades. Reports may show incorrect counts. Document history becomes unreliable. Cascade deletes don't work, leaving orphaned BLOB attachments consuming disk space.

**Prevention:**
1. Execute `PRAGMA foreign_keys = ON` immediately after every database connection is opened
2. In EF Core: add `PRAGMA foreign_keys = ON` to the connection string or execute in `OnConfiguring`
3. Add this PRAGMA to your `DbContext` constructor or factory:
```csharp
optionsBuilder.UseSqlite(connectionString);
// After connection opens:
dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
```
4. Write an integration test that verifies FK constraints are enforced (insert orphaned child, expect failure)

**Detection:** Deleting a document does NOT delete its associated movements/attachments. Foreign key violation does not throw an exception.

**Phase to address:** Phase 1 (Data Layer) — must be configured on DbContext initialization.

---

## Moderate Pitfalls

### Pitfall 7: Missing Duplicate Document Detection

**What goes wrong:** The same paper document is registered multiple times under different entries. Staff cannot distinguish between "same document, second entry" and "genuinely different document." Records become unreliable.

**Why it happens:** No unique constraint on document identifiers (file number, sender + date + subject combination). The system allows any string as a file number.

**Prevention:**
1. Enforce `UNIQUE` constraint on `FileNumber` (or `FileNumber + Year` if numbers recycle annually)
2. Implement fuzzy duplicate detection on save: check sender + date + subject similarity and warn the user
3. Add a `UNIQUE` constraint on `(SenderName, DocumentDate, Subject)` — not as a hard block, but as a soft warning to prevent accidental duplicates

**Detection:** Multiple records with same file number. Staff manually tracking duplicates in a separate notebook.

**Phase to address:** Phase 2 (Document Entry)

---

### Pitfall 8: Scanned Attachment Storage Bloat

**What goes wrong:** The SQLite database file grows to gigabytes because scanned document images are stored as BLOBs directly in the database. Backup times become excessive. Performance degrades.

**Why it happens:** Storing binary files (scanned PDFs/JPEGs) inside the database rather than as files on disk. While SQLite supports BLOBs (per FAQ #10), large BLOBs fragment the database and make VACUUM operations very slow.

**Prevention:**
1. Store scanned documents as files in a managed directory (e.g., `C:\FileTracker\Attachments\{DocumentId}\scan.pdf`)
2. Store only the file path and metadata (filename, size, mime type, scan date) in the database
3. Implement a maximum file size limit (e.g., 10MB per scan) with user-friendly error messages
4. Consider automatic compression (JPEG quality reduction) for oversized scans
5. Use a predictable, collision-resistant directory structure: `Attachments\{Year}\{Month}\{DocumentId}_{timestamp}.pdf`
6. The backup strategy must include the attachments directory alongside the database file

**Detection:** Database file grows beyond 100MB within weeks of use. `VACUUM` operations take minutes. Backup file is impractically large.

**Phase to address:** Phase 1 (Data Model) and Phase 2 (Document Entry with Scanning)

---

### Pitfall 9: No Reporting Infrastructure from Day One

**What goes wrong:** Monthly summary reports are treated as an afterthought. When staff first request a report, the data model doesn't support the queries needed, requiring schema changes and data migration.

**Why it happens:** The "generate monthly summary reports" requirement is deferred to a later phase. The initial data model focuses only on CRUD operations, not aggregate queries. Government offices specifically need periodic reports — this is not optional.

**Consequences:** Schema migration on live data. Reports produce incorrect data because the data model doesn't reliably capture "what was the state on March 31st?" versus "what is the current state?"

**Prevention:**
1. Design the database schema with reporting queries in mind from Phase 1
2. Include `CreatedAt` timestamps on ALL records — reports always filter by date range
3. Verify that "documents received in March 2026" is answerable with a single SQL query against the Phase 1 schema
4. Movement history must be timestamped so "where was document X on date Y?" is answerable
5. Build at least one report query as part of Phase 1 testing to validate the schema
6. Include export to Excel/PDF as a first-class feature (government offices need printed reports)

**Detection:** Phase 1 schema has no `DATETIME` columns beyond a single `CreatedDate`. No way to answer "how many documents moved from Registrar to Dean in February?"

**Phase to address:** Phase 1 (Schema must support reporting). Phase 4 (Report UI).

---

### Pitfall 10: WPF Memory Leaks from Event Handler Subscriptions

**What goes wrong:** The application's memory usage grows over hours of use. Eventually Windows reports low memory or the app becomes sluggish. Restarting "fixes" it temporarily.

**Why it happens:** WPF's event system creates strong references. If a ViewModel subscribes to a long-lived service's event and never unsubscribes, the ViewModel (and its View) cannot be garbage collected. Common culprits: `PropertyChanged` handlers, `CollectionChanged` handlers, static event handlers.

**Prevention:**
1. Always unsubscribe events in cleanup (implement `IDisposable` on ViewModels)
2. Use weak event patterns or `WeakReference` for long-lived event sources
3. Use the CommunityToolkit.Mvvm's `ObservableObject` which handles cleanup
4. Implement `Dispose()` on ViewModels that unsubscribe from all events
5. For `ObservableCollection`: clear and dispose when ViewModel is no longer needed
6. Test with long-running sessions (keep app open for 8+ hours and monitor memory)

**Detection:** Task Manager shows memory growing steadily over hours. Application gets slower over a workday. No dispose/unsubscribe calls in ViewModel cleanup.

**Phase to address:** Phase 2 (all ViewModel development)

---

### Pitfall 11: No Offline/Export Capability

**What goes wrong:** When the computer fails, is replaced, or the application needs to be reinstalled, staff cannot access document records. There is no portable format export. The database is trapped inside a SQLite file that requires the application to read.

**Why it happens:** All data is locked in SQLite. No export to CSV, Excel, or PDF. Government offices specifically need paper-compatible outputs for audits, inspections, and handovers.

**Prevention:**
1. Build PDF export for individual document records from Phase 2
2. Build Excel/CSV export for filtered lists and search results from Phase 3
3. Monthly reports must be exportable to PDF for printing and filing
4. Include a "Export All Data" function that produces a timestamped CSV/Excel dump
5. The exported format must include all essential fields — not a subset
6. Export should work even if the application is partially broken (fail gracefully)

**Detection:** No "Export" button anywhere. No PDF generation library in dependencies. Reports are view-only in the application.

**Phase to address:** Phase 3 (Search/Filter) and Phase 4 (Reports)

---

### Pitfall 12: No Unique Document Identifier Strategy

**What goes wrong:** Documents are registered without a system-enforced unique identifier. File numbers are manually entered and can be duplicated or omitted. Staff cannot reliably reference a specific document.

**Why it happens:** No `UNIQUE` constraint on file numbers. The system allows blank file numbers. No auto-generation of reference numbers.

**Prevention:**
1. Auto-generate a system ID (e.g., `REG/2026/0001` format: prefix/year/sequential) as the primary reference
2. Allow manual file number override but enforce UNIQUE constraint
3. Both system ID and manual file number should be searchable
4. Never allow blank/null file numbers — enforce at database level with `NOT NULL`
5. Format: `{Prefix}/{Year}/{SequentialNumber}` — sequential number resets per year

**Detection:** FileNumber column is nullable or has no UNIQUE constraint. Multiple documents share the same file number.

**Phase to address:** Phase 1 (Schema) and Phase 2 (Entry forms)

---

## Minor Pitfalls

### Pitfall 13: Date Handling Without Time Component

**What goes wrong:** Using C# `DateTime` without considering time components. Documents registered at 11:59 PM on March 31st might not appear in the March report. Time zone issues on a single-user desktop are rare but clock changes (DST) can cause ordering problems.

**Prevention:**
1. Use `DateOnly` (available in .NET 6+) for document dates that don't need time
2. Use `DateTimeOffset` or UTC `DateTime` for audit timestamps (`CreatedAt`, `ModifiedAt`)
3. Always store in UTC and convert to local time for display
4. For report date ranges: use inclusive start-of-day to end-of-day bounds, not midnight comparisons

**Phase to address:** Phase 1 (Data Model)

---

### Pitfall 14: ComboBox/DataGrid Performance with Growing Data

**What goes wrong:** Dropdown lists (officer selection, department selection) or DataGrids become sluggish as data grows because every item is loaded into memory. A DataGrid with 5,000 document rows becomes unusable.

**Prevention:**
1. Use WPF's built-in UI virtualization (`VirtualizingStackPanel.IsVirtualizing="True"` on DataGrid)
2. Implement server-side pagination: load 50-100 rows at a time, fetch more on scroll
3. For lookup ComboBoxes (officers, departments): load once, cache in memory — these are small datasets
4. Never bind a DataGrid directly to `context.Documents.ToList()` — use filtered, paged queries
5. Row virtualization requires fixed row heights — do NOT use `Auto` row height

**Phase to address:** Phase 3 (Search/Listing)

---

### Pitfall 15: Not Using MVVM Pattern

**What goes wrong:** Business logic, data access, and UI logic are mixed in code-behind files. The application becomes untestable and resistant to change. Adding a new feature requires touching XAML, code-behind, and database code in the same file.

**Why it happens:** WPF allows code-behind for rapid prototyping. Developers continue the pattern into production. Per Microsoft's [MVVM documentation](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm): "Complex maintenance issues can arise as apps are modified and grow in size and scope."

**Prevention:**
1. Use MVVM from Phase 1. Do not start with code-behind "temporarily"
2. Use CommunityToolkit.Mvvm NuGet package for source-generated `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`
3. ViewModels are the ONLY place database calls happen (via a Repository/Service layer)
4. Code-behind files should be empty except for `InitializeComponent()` — if they grow beyond 10 lines, something is wrong
5. Dependency injection: register DbContext, Services, and ViewModels in `App.xaml.cs` startup

**Detection:** More than 20 lines in any `.xaml.cs` file. SQL queries in code-behind. `new DbContext()` called from a XAML event handler.

**Phase to address:** Phase 1 (Project Structure) — MVVM is an architectural decision that shapes every subsequent phase.

---

### Pitfall 16: SQLite Type Affinity Confusion

**What goes wrong:** SQLite's [dynamic typing](https://www.sqlite.org/faq.html#q3) allows inserting a string into an INTEGER column without error. Invalid data enters the database silently. Validation must happen at the application level.

**Prevention:**
1. Never rely on SQLite column types for validation — always validate in C# before saving
2. Use EF Core data annotations (`[Required]`, `[MaxLength]`, `[Range]`) for model-level validation
3. Add WPF `ValidationRule` classes on input bindings for UI-level validation
4. Write integration tests that attempt to insert invalid data types and verify they're rejected

**Phase to address:** Phase 2 (Input Validation)

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| **Phase 1: Data Model** | Hard-coding officer hierarchy (Pitfall 3) | Store positions in config table with ordering |
| **Phase 1: Data Model** | Foreign keys disabled (Pitfall 6) | `PRAGMA foreign_keys = ON` in DbContext init |
| **Phase 1: Data Model** | No audit columns on tables (Pitfall 4) | Every table gets `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` |
| **Phase 1: Data Model** | Storing attachments as BLOBs (Pitfall 8) | Filesystem storage with path in DB |
| **Phase 1: Data Model** | Schema can't answer reporting queries (Pitfall 9) | Validate with sample report queries before completing the phase |
| **Phase 2: Document Entry** | UI thread blocking on save (Pitfall 5) | Async commands, background DB operations |
| **Phase 2: Document Entry** | Code-behind instead of MVVM (Pitfall 15) | Enforce MVVM from first ViewModel |
| **Phase 2: Document Entry** | No duplicate detection (Pitfall 7) | UNIQUE constraint on file number; fuzzy duplicate warning |
| **Phase 2: Document Entry** | Synchronous INSERT without transactions (Pitfall 2) | Batch saves in explicit transactions |
| **Phase 3: Search/Filter** | DataGrid loads all rows (Pitfall 14) | Virtualization + pagination |
| **Phase 3: Search/Filter** | No export capability (Pitfall 11) | Build Excel/CSV export from Phase 3 |
| **Phase 4: Reports** | Date boundary bugs (Pitfall 13) | Inclusive date range queries, `DateOnly` type |
| **Phase 4: Reports** | Reports in app only, no PDF (Pitfall 11) | PDF export for all report types |
| **Phase 5: Document Tracking** | Current location as mutable column (Pitfall 4) | Derived from most recent movement row |
| **General: Deployment** | No backup strategy (Pitfall 1) | Automated backup on close + periodic during use |

---

## Sources

- [SQLite Appropriate Uses](https://www.sqlite.org/whentouse.html) — MEDIUM confidence (official, current as of 2025-05-31)
- [SQLite How To Corrupt An SQLite Database File](https://www.sqlite.org/howtocorrupt.html) — HIGH confidence (official, current as of 2026-04-13)
- [SQLite Frequently Asked Questions](https://www.sqlite.org/faq.html) — HIGH confidence (official, current as of 2024-11-26)
- [SQLite Is Transactional](https://www.sqlite.org/transactional.html) — HIGH confidence (official)
- [EF Core Efficient Updating](https://learn.microsoft.com/en-us/ef/core/performance/efficient-updating) — HIGH confidence (official Microsoft, current as of 2025-01-15)
- [EF Core Performance Diagnosis](https://learn.microsoft.com/en-us/ef/core/performance/performance-diagnosis) — HIGH confidence (official Microsoft, current as of 2025-10-30)
- [EF Core Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) — HIGH confidence (official Microsoft)
- [WPF Data Binding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/) — HIGH confidence (official Microsoft, current as of 2025-08-27)
- [.NET MAUI MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm) — MEDIUM confidence (MAUI-specific but MVVM principles apply to WPF; official Microsoft, 2024-09-10)
