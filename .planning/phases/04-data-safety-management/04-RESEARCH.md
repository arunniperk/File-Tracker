# Phase 4: Data Safety & Management - Research

**Researched:** 2026-05-29
**Domain:** SQLite backup/restore, ZIP compression, database integrity verification
**Confidence:** HIGH (verified against official Microsoft Learn and SQLite docs)

## Summary

Phase 4 adds data safety to the File Tracker WPF application: manual one-click backup/restore, automatic daily backups on application close, and database integrity verification on startup. All three capabilities use APIs already available in the project's existing dependency set — **no new NuGet packages are required**.

Backup uses `Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase()` (the C# wrapper around SQLite's online backup API) to create a consistent snapshot of the live database, then `System.IO.Compression.ZipFile.CreateFromDirectory()` to bundle the backup .db file with the entire attachments directory into a single timestamped ZIP archive. Restore reverses this: extract ZIP, verify the extracted database with `PRAGMA integrity_check`, then replace the live files and restart.

Auto-backup on close uses the same backup pipeline to `%LocalAppData%\FileTracker\autobackups\` with a 7-file rolling retention policy. Startup integrity check runs `PRAGMA integrity_check` silently; only alerts the user (via MessageBox) if corruption is detected, at which point it offers to restore from the latest auto-backup.

**Primary recommendation:** Build a single `IBackupService` in `FileTracker.Core` that encapsulates both backup and restore logic. Leverage `SqliteConnection.BackupDatabase()` for zero-cost database copies (no SQL dump/reload), `ZipFile` for bundling, and execute `PRAGMA integrity_check` via a simple `ExecuteScalarAsync` call on the existing `SqliteConnection`. Keep the service layer in Core, UI orchestration in App (menu commands + startup/exit hooks in App.xaml.cs), and data-access helpers in FileTracker.Data.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Database backup (SQLite copy) | Data (`FileTracker.Data`) | Core (service interface) | `BackupDatabase()` requires a live `SqliteConnection` — data-layer concern. The service orchestrates the full backup pipeline. |
| ZIP compression / extraction | Core (`FileTracker.Core`) | — | `System.IO.Compression.ZipFile` is a pure I/O concern with no DB dependency — belongs in core service layer. |
| Integrity check (`PRAGMA integrity_check`) | Data (`FileTracker.Data`) | App (startup hook) | Executes against the live connection; helper method in `DatabaseInitializer`. Triggered on startup in `App.xaml.cs`. |
| Backup orchestration (DB + attachments → ZIP) | Core (service) | App (UI commands) | `IBackupService` in Core coordinates DB backup + file zipping. ViewModels/App call into it. |
| Restore orchestration (ZIP → DB + attachments) | Core (service) | App (UI commands) | Same service; handles extraction, integrity check, file replacement, and app restart. |
| Auto-backup on close | App (`App.xaml.cs`) | Core (service) | `OnExit` event handler calls `IBackupService`. |
| User-facing backup/restore UI | App (ViewModels) | — | Menu commands in MainWindow, folder picker dialog, warning MessageBox. |
| Settings (auto-backup toggle, retention) | App (`appsettings.json`) | — | Read via `Microsoft.Extensions.Configuration` on startup. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Data.Sqlite` | 9.0.16 (installed) | `BackupDatabase()` API + `PRAGMA integrity_check` execution | Already in project; provides the official .NET wrapper around SQLite's backup and pragma APIs |
| `System.IO.Compression.ZipFile` | Built-in (.NET 9) | `ZipFile.CreateFromDirectory()` and `ExtractToDirectory()` | Ships with the .NET runtime — no NuGet package needed. Battle-tested ZIP handling |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Configuration` | 9.0.16 (installed via Hosting) | Read auto-backup settings from `appsettings.json` | App startup — already used by the Generic Host |
| `Serilog` | 4.3.1 (installed) | Log integrity check results, backup success/failure | All backup/restore operations — already configured |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `System.IO.Compression.ZipFile` | SharpZipLib | SharpZipLib adds a NuGet dependency for functionality already in the BCL. Use built-in unless ZIP64 support (>4GB) is needed, which is unlikely for this app's data volume. |
| `SqliteConnection.BackupDatabase()` | `File.Copy` of .db file | `File.Copy` on a live, open WAL-mode database produces a hot/inconsistent copy. `BackupDatabase()` uses SQLite's online backup API which reads pages transactionally — the backup is guaranteed consistent. **Never use File.Copy on a live SQLite database.** |

**Installation:**
```bash
# No new packages required. All APIs are in already-installed dependencies.
```

**Version verification:**
```bash
# Verify existing packages are available
dotnet list package --project "C:\Project\File Tracker\src\FileTracker.Data\FileTracker.Data.csproj" | Select-String "Microsoft.Data.Sqlite"
dotnet list package --project "C:\Project\File Tracker\src\FileTracker.App\FileTracker.App.csproj" | Select-String "Serilog"
# System.IO.Compression.ZipFile is part of .NET 9 runtime — no package to verify
```

## Package Legitimacy Audit

No new packages are introduced in this phase. All required APIs are either:
- **Already installed:** `Microsoft.Data.Sqlite` 9.0.16, `Serilog` 4.3.1
- **Built into .NET 9 runtime:** `System.IO.Compression.ZipFile`, `System.IO.Compression.ZipFile.ExtractToDirectory`

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| *(none — no new packages)* | — | — | — | — | — | — |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        APPLICATION STARTUP                           │
│                                                                      │
│  App.xaml.cs: OnStartup                                              │
│    │                                                                 │
│    ├──► DatabaseInitializer.InitializeAsync()  (existing)            │
│    │                                                                 │
│    └──► NEW: IntegrityCheckAsync()                                   │
│           │                                                          │
│           ├── ExecuteScalarAsync("PRAGMA integrity_check")           │
│           │     returns "ok"?                                        │
│           │                                                          │
│           ├── YES → Log "integrity check passed" → continue          │
│           │                                                          │
│           └── NO  → Log error rows                                   │
│                     → MessageBox: "Database corruption detected.     │
│                        Restore from backup?"                         │
│                       ├── Yes → RestoreFromLatestAutoBackupAsync()   │
│                       └── No  → Continue (user accepts risk)         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      MANUAL BACKUP FLOW                              │
│                                                                      │
│  MainWindow → "File → Backup" menu item                              │
│    │                                                                 │
│    ▼                                                                 │
│  FolderBrowserDialog → user picks destination                        │
│    │                                                                 │
│    ▼                                                                 │
│  IBackupService.CreateBackupAsync(destinationPath)                   │
│    │                                                                 │
│    ├── 1. Create temp directory                                      │
│    ├── 2. Open new SqliteConnection to temp\filetracker_backup.db    │
│    ├── 3. sourceConn.BackupDatabase(tempConn)                        │
│    │       → SQLite online backup API                                │
│    │       → Produces consistent, transactional copy                 │
│    ├── 4. Close temp connection                                      │
│    ├── 5. Copy attachments/ dir into temp dir                        │
│    ├── 6. ZipFile.CreateFromDirectory(tempDir,                       │
│    │        "FileTracker_Backup_2026-05-29_143022.zip")              │
│    ├── 7. Delete temp directory                                      │
│    └── 8. Return path to created ZIP                                 │
│                                                                      │
│  → MessageBox: "Backup created: {path}"                              │
│  → Log via Serilog: "Backup created at {path}, size {bytes}"         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      MANUAL RESTORE FLOW                             │
│                                                                      │
│  MainWindow → "File → Restore" menu item                             │
│    │                                                                 │
│    ▼                                                                 │
│  OpenFileDialog (filter: *.zip) → user selects backup file           │
│    │                                                                 │
│    ▼                                                                 │
│  MessageBox: "This will replace all current data. Continue?"         │
│    ├── No → abort                                                    │
│    └── Yes →                                                         │
│           │                                                          │
│           ▼                                                          │
│  IBackupService.RestoreFromBackupAsync(zipPath)                      │
│    │                                                                 │
│    ├── 1. Extract ZIP to temp directory                              │
│    ├── 2. Open SqliteConnection to extracted .db file                │
│    ├── 3. Execute PRAGMA integrity_check on extracted DB             │
│    │       → fails? throw InvalidOperationException                  │
│    ├── 4. Close all connections to live database                     │
│    ├── 5. Replace live DB file with extracted backup DB              │
│    ├── 6. Delete existing attachments directory                      │
│    ├── 7. Copy extracted attachments to live attachments directory   │
│    ├── 8. Delete temp directory                                      │
│    └── 9. Signal restart needed                                      │
│                                                                      │
│  → MessageBox: "Restore complete. Application will restart."         │
│  → Application.Current.Shutdown() + System.Windows.Forms.            │
│       Application.Restart()                                          │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      AUTO-BACKUP ON CLOSE                            │
│                                                                      │
│  App.xaml.cs: OnExit                                                 │
│    │                                                                 │
│    ├── Check settings: AutoBackup enabled?                           │
│    │     └── No → skip                                               │
│    │                                                                 │
│    └── Yes →                                                         │
│           │                                                          │
│           ▼                                                          │
│  IBackupService.CreateAutoBackupAsync()                              │
│    │                                                                 │
│    ├── Destination: %LocalAppData%\FileTracker\autobackups\          │
│    │                 FileTracker_AutoBackup_YYYY-MM-DD_HHmmss.zip    │
│    ├── Same backup pipeline as manual (BackupDatabase + ZipFile)     │
│    └── Enforce 7-file rolling retention:                             │
│          ├── List *.zip in autobackups\ sorted by date               │
│          ├── If count > 7 → delete oldest until count == 7           │
│          └── Log each deleted file                                   │
└─────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure (new files only)
```
src/
├── FileTracker.Core/
│   └── Services/
│       └── IBackupService.cs           # Backup/restore contract
├── FileTracker.Data/
│   └── BackupService.cs                # Implementation: BackupDatabase + ZipFile + integrity_check
└── FileTracker.App/
    ├── appsettings.json                # Add: AutoBackup section
    ├── Views/
    │   └── SettingsWindow.xaml/.cs     # NEW: auto-backup toggle UI (if not integrating into existing)
    ├── ViewModels/
    │   └── SettingsViewModel.cs        # NEW: auto-backup settings binding
    └── App.xaml.cs                     # Modified: integrity check on startup, auto-backup on exit
```

### Pattern 1: Service Interface + Implementation
**What:** `IBackupService` in `FileTracker.Core.Services` defines the backup contract. Implementation in `FileTracker.Data` (or a services subfolder) because it requires `SqliteConnection`. The interface enables injection into ViewModels and `App.xaml.cs`.

**When to use:** All backup/restore operations. Keeps the SQLite-specific logic isolated. Enables testing with a mock.

**Example:**
```csharp
// Source: Microsoft.Data.Sqlite official docs [VERIFIED: learn.microsoft.com]
// FileTracker.Core/Services/IBackupService.cs
namespace FileTracker.Core.Services;

public interface IBackupService
{
    /// <summary>Creates a full backup ZIP at the specified path.</summary>
    Task<string> CreateBackupAsync(string destinationDirectory, CancellationToken ct = default);
    
    /// <summary>Restores from a backup ZIP file.</summary>
    Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default);
    
    /// <summary>Creates an auto-backup to the configured autobackup directory.</summary>
    Task CreateAutoBackupAsync(CancellationToken ct = default);
    
    /// <summary>Runs PRAGMA integrity_check on the live database.</summary>
    Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken ct = default);
}

public record IntegrityCheckResult(bool IsOk, IReadOnlyList<string> Errors);
```

### Pattern 2: BackupDatabase for Consistent Snapshots
**What:** Use `SqliteConnection.BackupDatabase()` (the .NET wrapper over `sqlite3_backup_init/step/finish`) to create a transactional, consistent copy of the live database. The destination must be a separate `SqliteConnection` pointing to a different file.

**When to use:** Every backup operation. Never use `File.Copy` on a live WAL-mode database.

**Example:**
```csharp
// Source: Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase [VERIFIED: learn.microsoft.com]
// FileTracker.Data/BackupService.cs (excerpt)
private async Task BackupDatabaseToFileAsync(string destinationPath)
{
    // Build connection string for the backup file
    var backupCs = new SqliteConnectionStringBuilder
    {
        DataSource = destinationPath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    using var destConn = new SqliteConnection(backupCs);
    await destConn.OpenAsync();
    
    // BackupDatabase requires both connections open
    _sourceConnection.BackupDatabase(destConn);
    // destConn now contains a consistent snapshot
}
```

### Pattern 3: PRAGMA integrity_check
**What:** Execute `PRAGMA integrity_check` via `ExecuteScalarAsync()`. Returns the string `"ok"` on success. On failure, returns error strings — use `ExecuteReaderAsync()` to capture all error rows. The check does low-level b-tree validation, missing page detection, and freelist integrity.

**When to use:** On application startup (silently unless corruption detected) and on the extracted backup before restore.

**Example:**
```csharp
// Source: SQLite official docs [VERIFIED: sqlite.org/pragma.html#pragma_integrity_check]
public async Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken ct = default)
{
    await using var cmd = _sourceConnection.CreateCommand();
    cmd.CommandText = "PRAGMA integrity_check";
    
    var result = await cmd.ExecuteScalarAsync(ct);
    if (result is string s && s.Equals("ok", StringComparison.OrdinalIgnoreCase))
    {
        return new IntegrityCheckResult(true, Array.Empty<string>());
    }
    
    // Re-execute to get all error rows
    var errors = new List<string>();
    cmd.CommandText = "PRAGMA integrity_check"; // fresh command
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        errors.Add(reader.GetString(0));
    }
    return new IntegrityCheckResult(false, errors);
}
```

### Pattern 4: ZIP with Attachments Bundling
**What:** Create a temp directory, copy the backup .db + entire attachments tree into it, then `ZipFile.CreateFromDirectory()` with `CompressionLevel.Optimal`. Use `includeBaseDirectory: false` so the ZIP contains `filetracker_backup.db` and `attachments/` at the root.

**When to use:** Final step of the backup pipeline.

**Example:**
```csharp
// Source: System.IO.Compression.ZipFile.CreateFromDirectory [VERIFIED: learn.microsoft.com]
var tempDir = Path.Combine(Path.GetTempPath(), $"FileTracker_Backup_{DateTime.Now:yyyyMMdd_HHmmss}");
Directory.CreateDirectory(tempDir);

// Copy backup DB
var backupDbPath = Path.Combine(tempDir, "filetracker_backup.db");
await BackupDatabaseToFileAsync(backupDbPath);

// Copy attachments (if they exist)
var attachmentsSource = _attachmentRoot; // %LocalAppData%\FileTracker\attachments
if (Directory.Exists(attachmentsSource))
{
    CopyDirectory(attachmentsSource, Path.Combine(tempDir, "attachments"));
}

// ZIP it
var zipName = $"FileTracker_Backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip";
var zipPath = Path.Combine(destinationDirectory, zipName);
ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

// Cleanup
Directory.Delete(tempDir, recursive: true);
```

### Pattern 5: Restore with Safety Checks
**What:** Extract ZIP to temp dir → integrity check on extracted DB → close live connection → replace files → signal restart. **Critical:** The integrity check on the extracted backup MUST pass before replacing live data.

**When to use:** Restore operation. Must be preceded by a user warning dialog.

**Example:**
```csharp
// Source: Architected pattern — composite of verified APIs
public async Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default)
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"FileTracker_Restore_{Guid.NewGuid():N}");
    ZipFile.ExtractToDirectory(backupFilePath, tempDir);

    var extractedDbPath = Path.Combine(tempDir, "filetracker_backup.db");
    if (!File.Exists(extractedDbPath))
        throw new InvalidOperationException("Backup ZIP does not contain a database file.");

    // Verify integrity of the backup DB before restoring
    var backupCs = new SqliteConnectionStringBuilder
    {
        DataSource = extractedDbPath,
        Mode = SqliteOpenMode.ReadOnly
    }.ToString();
    using var checkConn = new SqliteConnection(backupCs);
    await checkConn.OpenAsync(ct);
    await using var cmd = checkConn.CreateCommand();
    cmd.CommandText = "PRAGMA integrity_check";
    var result = await cmd.ExecuteScalarAsync(ct);
    if (result is not string s || !s.Equals("ok", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Backup database is corrupt and cannot be restored.");

    // Close live database (dispose the singleton connection)
    // Replace files...
    // ... then restart app
}
```

### Anti-Patterns to Avoid
- **File.Copy on live WAL database:** Produces an inconsistent snapshot. WAL mode has separate -wal and -shm files that File.Copy may miss or capture mid-write. Always use `BackupDatabase()`.
- **BackupDatabase without destination OpenAsync:** Both connections must be in `Open` state. The method throws if the destination is closed.
- **Using ExecuteReader for 'ok' check:** `PRAGMA integrity_check` returns `"ok"` as a single row — `ExecuteScalarAsync` is sufficient for the success path. Use `ExecuteReaderAsync` only for error collection on failure.
- **Restoring without integrity check on extracted DB:** A corrupt backup can silently replace good live data. Always validate extracted DB before replacing.
- **Zipping the attachments directory while the app holds file handles:** Backup is safe because `BackupDatabase` handles locking. Attachment files are read-only copied — but if a user is actively uploading an attachment during backup, the backup may capture a partial file. Mitigate by logging a warning; this is an acceptable edge case for a single-user app.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Consistent DB snapshot | `File.Copy(sourceDb, destDb)` | `SqliteConnection.BackupDatabase(destConn)` | `File.Copy` on a live WAL-mode database produces an incomplete/inconsistent copy (missing WAL pages). `BackupDatabase` uses the SQLite online backup API which reads pages transactionally. |
| ZIP compression | Custom ZIP library or manual entry creation | `System.IO.Compression.ZipFile.CreateFromDirectory()` | Ships with .NET. Handles edge cases: path traversal prevention, encoding, empty directories, large files. Zero-dependency. |
| Database integrity verification | Custom checksum / row counting | `PRAGMA integrity_check` | SQLite's built-in pragma checks b-tree structure, page linkage, freelist integrity, and UNIQUE/NOT NULL constraint violations. Far more thorough than any hand-rolled check. |
| Rolling file retention | Custom file cleanup logic | Manual `Directory.GetFiles().OrderBy().Skip(7)` — simple, safe | This is a 5-line operation using standard `System.IO` APIs. No library needed. Just be careful to catch `IOException` during deletion. |
| App restart after restore | Custom process management | `System.Windows.Application.Current.Shutdown()` then `System.Windows.Forms.Application.Restart()` | Standard .NET pattern for WPF app restart. `Application.Restart()` is in `System.Windows.Forms.dll` (reference needed in .csproj). |

**Key insight:** The SQLite backup API (`BackupDatabase`) is the critical piece that cannot be replaced by simpler approaches. Attempting `File.Copy` on a WAL-mode database is the #1 existential risk for this phase per the project's own PITFALLS.md (Pitfall 1). The `BackupDatabase` method is battle-tested and used by high-profile .NET apps.

## Common Pitfalls

### Pitfall 1: BackupDatabase with WAL Mode Requires Both Connections Open
**What goes wrong:** Calling `BackupDatabase()` when either the source or destination connection is closed throws `InvalidOperationException`.
**Why it happens:** The SQLite online backup API (`sqlite3_backup_init`) requires both database handles to be open and valid. Microsoft.Data.Sqlite mirrors this constraint.
**How to avoid:** Always `Open()` or `OpenAsync()` the destination connection before calling `BackupDatabase()`. Use `using` blocks to ensure the destination connection is disposed (which closes it).
**Warning signs:** `InvalidOperationException: "Connection must be open"` or SQLite error 21 (SQLITE_MISUSE).

### Pitfall 2: ZIP Destination Already Exists
**What goes wrong:** `ZipFile.CreateFromDirectory()` throws `IOException` if the destination ZIP file already exists.
**Why it happens:** The method does not overwrite by default — this is a safety feature.
**How to avoid:** Either (a) append a timestamp to the filename to guarantee uniqueness, or (b) delete the existing file before calling `CreateFromDirectory`. The timestamp approach (already in the naming convention per D-03) is safer.
**Warning signs:** `IOException: "The file '...' already exists."` during backup creation.

### Pitfall 3: integrity_check Returning 'ok' with Whitespace/Encoding Differences
**What goes wrong:** Comparing the result of `PRAGMA integrity_check` with the literal string `"ok"` using `==` may fail if SQLite returns the value with extra whitespace.
**Why it happens:** Different SQLite builds or connection configurations may include trailing characters. The official SQLite docs state the return is the string `'ok'` — but safe comparison handles whitespace.
**How to avoid:** Use `result?.ToString()?.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase)`. This handles any whitespace or encoding variance.
**Warning signs:** Integrity check on a known-good database returns "not ok" due to string comparison failure.

### Pitfall 4: Restoring While Database Is in Use
**What goes wrong:** Attempting to replace the live database file while the `SqliteConnection` singleton still holds an open handle causes `IOException` (file in use).
**Why it happens:** The `SqliteConnection` is registered as a singleton in DI and opened in `App.xaml.cs:OnStartup`. It stays open for the app's lifetime.
**How to avoid:** Before replacing files during restore: call `Close()` or `Dispose()` on the singleton `SqliteConnection`, perform the file replacements, then restart the application. The connection will be re-created on restart.
**Warning signs:** `IOException: "The process cannot access the file because it is being used by another process."`

### Pitfall 5: Auto-Backup Fires During Restore Shutdown
**What goes wrong:** When restoring, the app shuts down (to restart), which triggers `OnExit` → auto-backup. The auto-backup backs up a potentially partially-restored (or soon-to-be-replaced) database.
**Why it happens:** `OnExit` fires on both normal close and restore-triggered shutdown.
**How to avoid:** Set a flag (`_isRestoring`) before triggering shutdown for restore. In `OnExit`, skip auto-backup if `_isRestoring` is true. Log that auto-backup was skipped due to restore-in-progress.
**Warning signs:** Auto-backup file created at the exact time of a restore operation.

## Code Examples

Verified patterns from official sources:

### SQLite Backup via BackupDatabase
```csharp
// Source: Microsoft.Data.Sqlite.SqliteConnection.BackupDatabase [VERIFIED: learn.microsoft.com]
// Full backup of the main database to a new file
var sourceCs = new SqliteConnectionStringBuilder
{
    DataSource = @"C:\Data\filetracker.db",
    Mode = SqliteOpenMode.ReadWriteCreate
}.ToString();

using var sourceConn = new SqliteConnection(sourceCs);
sourceConn.Open();

var destCs = new SqliteConnectionStringBuilder
{
    DataSource = @"C:\Backups\filetracker_backup.db",
    Mode = SqliteOpenMode.ReadWriteCreate
}.ToString();

using var destConn = new SqliteConnection(destCs);
destConn.Open();

// Online backup — transactional, consistent snapshot
sourceConn.BackupDatabase(destConn);
// destConn now holds a consistent copy
```

### ZIP Creation with Attachments
```csharp
// Source: System.IO.Compression.ZipFile [VERIFIED: learn.microsoft.com]
using System.IO.Compression;

string startPath = @"C:\temp\backup_staging";
string zipPath = @"C:\Backups\FileTracker_Backup_2026-05-29_143022.zip";

ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
```

### ZIP Extraction with Overwrite
```csharp
// Source: System.IO.Compression.ZipFile.ExtractToDirectory [VERIFIED: learn.microsoft.com]
ZipFile.ExtractToDirectory(
    @"C:\Backups\FileTracker_Backup_2026-05-29_143022.zip",
    @"C:\temp\restore_staging",
    overwriteFiles: true);
```

### Integrity Check on Startup
```csharp
// Source: SQLite PRAGMA integrity_check [VERIFIED: sqlite.org/pragma.html#pragma_integrity_check]
await using var cmd = dbConnection.CreateCommand();
cmd.CommandText = "PRAGMA integrity_check";
var result = await cmd.ExecuteScalarAsync();

if (result?.ToString()?.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase) == true)
{
    _logger.LogInformation("Database integrity check passed");
}
else
{
    _logger.LogError("Database integrity check FAILED");
    // Collect error rows
    cmd.CommandText = "PRAGMA integrity_check";
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        _logger.LogError("Integrity error: {Error}", reader.GetString(0));
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `File.Copy` of SQLite .db file | `SqliteConnection.BackupDatabase()` | SQLite 3.6.11 (2009) — the online backup API | `BackupDatabase` produces a consistent transaction-level snapshot. `File.Copy` on a WAL-mode database can produce a corrupt copy. |
| SharpZipLib / Ionic.Zip | `System.IO.Compression.ZipFile` | .NET Framework 4.5 (2012) / .NET Core 1.0 | BCL ships ZIP support. No third-party dependency needed for standard ZIP operations. |
| Manual SQL dump + reload for backup | SQLite backup API | Always available since SQLite 3.x | Backup API is zero-SQL, page-level copy. Much faster than dump/reload for databases of any size. |

**Deprecated/outdated:**
- **SharpZipLib for basic ZIP:** The BCL `System.IO.Compression.ZipFile` handles standard ZIP creation/extraction. Use SharpZipLib only if you need ZIP64 (>4GB archives), AES encryption, or streaming partial extraction.
- **`File.Copy` for SQLite backup:** Never correct for a live database in WAL mode. The WAL file contains uncommitted pages that `File.Copy` may miss. `BackupDatabase()` is the only correct approach.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | No new NuGet packages are needed — all required APIs (BackupDatabase, ZipFile, PRAGMA integrity_check) are available in already-installed packages or the .NET 9 BCL | Standard Stack | LOW — verified by checking existing .csproj files and Microsoft Learn docs for API availability |
| A2 | The attachment root directory is `%LocalAppData%\FileTracker\attachments\` and attachments are stored in `{docId}\` subdirectories | Architecture Patterns | LOW — confirmed from AttachmentService.cs line 43-46 |
| A3 | The database path is `%LocalAppData%\FileTracker\filetracker.db` | Architecture Patterns | LOW — confirmed from App.xaml.cs line 46-48 |
| A4 | `System.Windows.Forms.Application.Restart()` is available on .NET 9-windows target and will restart the app correctly | Architecture Patterns | MEDIUM — `System.Windows.Forms.dll` may need an explicit `<PackageReference>` or `<FrameworkReference>` in the WPF .csproj. Plan should verify this. |

## Open Questions

1. **Settings UI approach — new window or integrate into existing?**
   - What we know: The app currently has no settings window. The only setting needed is the auto-backup toggle (enabled/disabled).
   - What's unclear: Whether to create a dedicated Settings window or add a checkbox to an existing UI (e.g., the Dashboard or a "File" menu toggle).
   - Recommendation: Use a simple checkbox in a new "File → Settings" menu item that opens a small dialog window. This keeps it discoverable without cluttering the main UI. Alternatively, a `CheckBox` directly in the "File" menu (WPF supports this) if the only setting is the toggle.

2. **Application restart mechanism for .NET 9 WPF**
   - What we know: `Application.Restart()` is in `System.Windows.Forms.dll`. The WPF project targets `net9.0-windows` which has access to WinForms APIs.
   - What's unclear: Whether `Application.Restart()` works correctly in a WPF app using the Generic Host pattern (the host needs to be properly disposed before restart, and the new process must re-read `appsettings.json`).
   - Recommendation: Test this early. Fallback: use `Process.Start(Process.GetCurrentProcess().MainModule.FileName)` then `Environment.Exit(0)`.

3. **Backup of WAL and SHM files**
   - What we know: `BackupDatabase()` reads pages from the WAL as part of the backup — the backup itself is a clean, rolled-up database without WAL/SHM sidecars. The backup destination file is a standard single-file SQLite database.
   - What's unclear: Whether the attachments directory may have files locked by the OS during backup (e.g., if a user has a file open in an external viewer).
   - Recommendation: Wrap the attachments copy step in a try-catch. Log warnings for individual file copy failures but continue the backup. A partial attachments backup is better than no backup at all.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 9 SDK | Build/compile | ✓ (inferred from existing project) | 9.0.x | — |
| `Microsoft.Data.Sqlite` | BackupDatabase, integrity_check | ✓ (installed) | 9.0.16 | — |
| `System.IO.Compression.ZipFile` | ZIP creation/extraction | ✓ (BCL) | Built-in | — |
| `System.Windows.Forms.dll` | `Application.Restart()` | ✓ (via `net9.0-windows` TFM) | Built-in | Manual `Process.Start` fallback |

**Missing dependencies with no fallback:** None — all required APIs are available.
**Missing dependencies with fallback:** `Application.Restart()` — fallback is `Process.Start` + `Environment.Exit`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 (inferred from STACK.md) |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Backup"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DATA-01 | Manual backup creates valid ZIP with DB + attachments | integration | `dotnet test --filter "BackupServiceTests.CreateBackup_ProducesValidZip"` | ❌ Wave 0 |
| DATA-01 | Backup ZIP can be extracted and database passes integrity_check | integration | `dotnet test --filter "BackupServiceTests.Backup_DatabaseIsConsistent"` | ❌ Wave 0 |
| DATA-02 | Restore from backup ZIP replaces live database | integration | `dotnet test --filter "BackupServiceTests.Restore_ReplacesDatabase"` | ❌ Wave 0 |
| DATA-02 | Restore rejects corrupt backup ZIP (integrity check fails) | unit | `dotnet test --filter "BackupServiceTests.Restore_RejectsCorruptBackup"` | ❌ Wave 0 |
| DATA-03 | Auto-backup on close creates timestamped backup | integration | `dotnet test --filter "BackupServiceTests.AutoBackup_CreatesFile"` | ❌ Wave 0 |
| DATA-03 | Rolling retention keeps last 7 backups | unit | `dotnet test --filter "BackupServiceTests.AutoBackup_EnforcesRetention"` | ❌ Wave 0 |
| D-09 | Startup integrity check returns 'ok' on clean database | integration | `dotnet test --filter "IntegrityCheckTests.Check_CleanDb_ReturnsOk"` | ❌ Wave 0 |
| D-09 | Startup integrity check detects corruption | integration | `dotnet test --filter "IntegrityCheckTests.Check_CorruptDb_ReturnsErrors"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Backup|IntegrityCheck"` 
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** All tests green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/FileTracker.Tests/Services/BackupServiceTests.cs` — covers DATA-01, DATA-02, DATA-03
- [ ] `tests/FileTracker.Tests/Services/IntegrityCheckTests.cs` — covers D-09
- [ ] `tests/FileTracker.Tests/` directory may need creation if not existing
- [ ] Test project `.csproj` may need `Microsoft.Data.Sqlite` reference for creating test databases

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Single-user desktop app — not applicable |
| V3 Session Management | no | Not applicable |
| V4 Access Control | no | Single-user — not applicable |
| V5 Input Validation | yes | Validate backup file paths (no path traversal), validate ZIP contents (no escape from extraction directory — `ExtractToDirectory` handles this natively) |
| V6 Cryptography | no | No encryption needed for local backup files |
| V7 Error Handling | yes | Backup/restore failures must log via Serilog without exposing stack traces in MessageBox. Catch and surface user-friendly messages. |

### Known Threat Patterns for WPF + SQLite + ZIP

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal in ZIP extraction (specially crafted backup with `../../` entries) | Tampering | `ZipFile.ExtractToDirectory` throws `IOException` if extraction would create files outside the destination directory [VERIFIED: learn.microsoft.com]. This is built-in protection. |
| Restoring a malicious backup that overwrites system files | Tampering | Restore only replaces files in known paths (`%LocalAppData%\FileTracker\`). Never extract to arbitrary locations. Validate backup integrity before restore. |
| Denial of service via oversized backup ZIP | DoS | `ZipFile.ExtractToDirectory` doesn't limit uncompressed size by default. For restore, add a pre-extraction size check or iterate entries manually. For manual backup, the user selects the destination — file size is their responsibility. |
| Information disclosure via backup file left in temp directory | Info Disclosure | Always delete the temp staging directory after ZIP creation. Use `try/finally` to ensure cleanup even on failure. |
| SQL injection via crafted backup filename | Tampering | Filenames are derived from timestamps (`DateTime.Now.ToString("yyyy-MM-dd_HHmmss")`) — never from user input. No SQL injection vector exists. |

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: SqliteConnection.BackupDatabase Method](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase) — verified API signature (two overloads), requires both connections open
- [Microsoft Learn: ZipFile.CreateFromDirectory Method](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory) — verified overloads, compression levels, `includeBaseDirectory` parameter
- [Microsoft Learn: ZipFile.ExtractToDirectory Method](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.extracttodirectory) — verified overloads, `overwriteFiles` parameter, path traversal prevention
- [SQLite Official Docs: PRAGMA integrity_check](https://www.sqlite.org/pragma.html#pragma_integrity_check) — verified return format ('ok' or error strings), max errors parameter, what it checks

### Secondary (MEDIUM confidence)
- Project codebase: `App.xaml.cs` — verified DB path, connection setup, existing DI patterns
- Project codebase: `AttachmentService.cs` — verified attachment root path (`%LocalAppData%\FileTracker\attachments`)
- Project codebase: `MainWindow.xaml` — verified existing UI structure (TabControl, no menu bar currently)
- `.planning/research/PITFALLS.md` — verified Pitfall 1 (backup strategy for SQLite)

### Tertiary (LOW confidence)
- None — all claims are verified against official documentation or the existing codebase.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all APIs verified on Microsoft Learn; no new packages required
- Architecture: HIGH — patterns follow existing project conventions (DI, service/repo separation, MVVM) verified in codebase
- Pitfalls: HIGH — verified against SQLite official docs and Microsoft.Data.Sqlite API reference
- Integrity check: HIGH — PRAGMA behavior verified on sqlite.org

**Research date:** 2026-05-29
**Valid until:** 2026-06-29 (30 days — stable APIs in .NET LTS)

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DATA-01 | User can backup the database and attachments to a chosen location | `BackupDatabase()` + `ZipFile.CreateFromDirectory()` + folder picker → fully supported |
| DATA-02 | User can restore from a backup file | `ZipFile.ExtractToDirectory()` + integrity check + file replacement + app restart → fully supported |
| DATA-03 | Application auto-creates daily backup on close (configurable) | `OnExit` hook + `IBackupService.CreateAutoBackupAsync()` + rolling retention → fully supported |
| D-09 | On startup, run PRAGMA integrity_check; warn if corruption | `ExecuteScalarAsync("PRAGMA integrity_check")` + MessageBox integration → fully supported |
| D-10 | Integrity check result logged via Serilog | `ILogger<BackupService>` injected via DI, already configured in app → fully supported |
