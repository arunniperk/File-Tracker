# Phase 4: Data Safety & Management - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

One-click backup and restore of the database and attachments directory, automatic daily backup on close, and database integrity verification on startup.

</domain>

<decisions>
## Implementation Decisions

### Backup
- **D-01:** Backup creates a timestamped ZIP or folder copy containing the SQLite database file + entire attachments directory
- **D-02:** User chooses backup destination via folder picker dialog
- **D-03:** Backup files named: FileTracker_Backup_YYYY-MM-DD_HHmmss.zip or similar

### Restore
- **D-04:** User selects a backup file, system confirms with warning dialog, then replaces current database and attachments
- **D-05:** App restarts after restore to reload the restored database

### Auto-Backup
- **D-06:** On application close, if enabled, automatically backup to %LocalAppData%\FileTracker\autobackups\
- **D-07:** Keep last 7 auto-backups, delete older ones (rolling)
- **D-08:** Auto-backup is enabled by default, configurable in settings

### Integrity Check
- **D-09:** On startup, run SQLite PRAGMA integrity_check. If corruption detected, warn user and offer restore from backup.
- **D-10:** Integrity check result logged via Serilog

### Claude's Discretion
- ZIP library choice (System.IO.Compression built-in or SharpZipLib)
- Settings UI approach (simple window or integrate into existing)
- Exact restoration procedure and safety checks

</decisions>

<canonical_refs>
## Canonical References

- `.planning/PROJECT.md` — Project context
- `.planning/REQUIREMENTS.md` — DATA-01, DATA-02, DATA-03
- `.planning/research/PITFALLS.md` — SQLite backup pitfall (#1 existential risk)
- `.planning/research/STACK.md` — Tech stack

</canonical_refs>

<code_context>
## Existing Code Insights

### Integration Points
- App.xaml.cs: Add integrity check on startup, auto-backup on Exit
- MainWindow: Add Backup/Restore menu items
- DatabaseInitializer: Add integrity check method
- appsettings.json: Add auto-backup settings

### Existing Patterns
- Service/repository DI pattern
- MessageBox for user confirmations
- Serilog for logging

</code_context>

<specifics>
## Specific Ideas

- Backup must be simple enough that non-technical staff can use it
- Integrity check on startup is silent unless corruption detected

</specifics>

<deferred>
## Deferred Ideas

None — this is the final phase of v1

</deferred>

---
*Phase: 04-data-safety-management*
*Context gathered: 2026-05-29*
