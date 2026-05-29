# Walking Skeleton — File Tracker

**Phase:** 1
**Generated:** 2026-05-29

## Capability Proven End-to-End

A registrar staff member launches the WPF desktop app and registers an incoming document (sender, subject, date, file number, remarks) that is persisted to SQLite and survives application restart.

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Framework | .NET 10.0 WPF (net10.0-windows TFM) | Native Windows 11 Fluent theme, battle-tested for LOB data-entry apps. C# 14, 3-year LTS support |
| Data layer | SQLite via Microsoft.Data.Sqlite + Dapper 2.1.79 | Zero-install file-based DB. Dapper provides raw SQL control with automatic parameterized mapping. ~10-15MB lighter than EF Core. WAL mode enabled |
| ORM | Dapper (NOT sqlite-net-pcl) | Locked STACK.md decision. All data access uses `QueryAsync<T>()` / `ExecuteAsync()` extension methods on `SqliteConnection`. Do NOT install sqlite-net-pcl |
| DI host | Microsoft.Extensions.Hosting — Host.CreateApplicationBuilder() | Modern pattern. Registers all services, ViewModels, and Views. WPF owns the dispatcher — `host.StartAsync()` called, NOT `host.RunAsync()` |
| Architecture | MVVM + Layered (Presentation → Application Service → Data Repository) | CommunityToolkit.Mvvm 8.4.2 source generators eliminate INPC boilerplate. Views bind to ViewModels. ViewModels delegate to Services. Services orchestrate via Repositories |
| Auth | None | Single-user desktop app for one Registrar desk. No authentication, no sessions, no access control |
| Deployment target | Single-file self-contained .exe (Phase 4) | No runtime dependency required. MSIX packaging deferred |
| Directory layout | Solution with 3 src projects + 1 test project | `FileTracker.App/` (WPF Views+ViewModels), `FileTracker.Core/` (Models+Services+DTOs), `FileTracker.Data/` (Repository+DatabaseInitializer), `tests/FileTracker.Tests/` (xunit.v3) |
| Database location | `%LocalAppData%\FileTracker\filetracker.db` | User-specific, not shared. Locked STACK.md decision |
| Logging | Serilog 4.3.1 → rolling file at `%LocalAppData%\FileTracker\logs\` | Structured logging. 7-day retention. Replaces default Microsoft loggers |
| Source generators | CommunityToolkit.Mvvm `[ObservableProperty]` + `[RelayCommand]` + `ObservableValidator` | Eliminates >80% MVVM boilerplate. Auto-generates INPC, ICommand, INotifyDataErrorInfo |

## Stack Touched in Phase 1

- [x] Project scaffold — 3 classlibs + 1 WPF app + 1 test project
- [x] Routing — single-window WPF app, no multi-page routing needed
- [x] Database — Documents, TrackingSequence, DocumentAudit tables with Dapper CRUD
- [x] UI — RegisterDocumentView with Incoming/Outgoing toggle, subject/date/file number/remarks fields
- [x] Deployment — `dotnet run --project src/FileTracker.App` launches the app locally

## Out of Scope (Deferred to Later Slices)

- Department, Priority, Document Type fields — Phase 2
- Configurable file number format (IITDH/REG/YYYY/NNNN) — Phase 2
- Officer hierarchy and movement tracking — Phase 2
- Document search and filtering — Phase 2
- Scanned document attachments — Phase 3
- Monthly reports and PDF/Excel export — Phase 3
- Operational dashboard — Phase 3
- Backup and restore — Phase 4
- Database integrity check on startup — Phase 4
- Multi-user, authentication, network access — Out of scope permanently
- Email notifications, barcode/QR — v2 deferred

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural decisions:

- Phase 2: Search, officer hierarchy, and movement tracking
- Phase 3: Dashboard, monthly reports, and document attachments
- Phase 4: Data safety (backup, restore, integrity checks)
