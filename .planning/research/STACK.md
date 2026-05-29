# Technology Stack

**Project:** File Tracker — IIT Dharwad Registrar Office Document Tracking System  
**Platform:** Windows 11 WPF Desktop Application  
**Researched:** 2026-05-29  
**Confidence:** HIGH

## Recommended Stack

### Runtime & Framework

| Component | Choice | Version | Rationale |
|-----------|--------|---------|-----------|
| Runtime | .NET | 10.0 (LTS) | Released Nov 2025, 3-year support, C# 14. All ecosystem packages target net10.0 |
| UI Framework | WPF | Built-in | Native Windows 11 Fluent theme with `ThemeMode` property (Light/Dark/System). No third-party theming needed |
| UI Toolkit | CommunityToolkit.Mvvm | 8.4.2 | Undisputed MVVM standard — used by PowerToys, Files (43K stars), DevToys. Source generators: `[ObservableProperty]`, `[RelayCommand]` |

### Data Access

| Component | Choice | Version | Rationale |
|-----------|--------|---------|-----------|
| Database | SQLite (via Microsoft.Data.Sqlite) | 10.0 | Zero-install, file-based, perfect for single-user desktop. Built-in .NET provider |
| ORM | Dapper | 2.1+ | 10x lighter than EF Core, full SQL control, automatic mapping. Production-proven (Bitwarden, Sonarr, Radarr). EF Core adds 10-15MB and slower cold starts |

### Infrastructure

| Component | Choice | Version | Rationale |
|-----------|--------|---------|-----------|
| DI Host | Microsoft.Extensions.Hosting | 10.0 | Standard .NET Generic Host with DI, configuration, logging |
| Logging | Serilog | 4.2+ | Structured logging to file. 2.8B+ downloads, used by PowerToys |
| Configuration | Microsoft.Extensions.Configuration | 10.0 | appsettings.json support via Generic Host |
| Behaviors | Microsoft.Xaml.Behaviors.Wpf | 10.0 | Official Microsoft package for WPF behaviors (EventToCommand, etc.) |

### Testing

| Component | Choice | Version | Rationale |
|-----------|--------|---------|-----------|
| Test Framework | xunit.v3 | 3.2.2 | xunit v2 is deprecated and no longer maintained. v3 integrates with Microsoft Testing Platform |
| Assertions | FluentAssertions | 7.2+ | Readable, chainable assertions for readability |
| Moq | Moq | 4.20+ | Standard mocking framework for .NET |

### Packaging & Deployment

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Primary | MSIX | Modern Windows packaging format, clean install/uninstall, auto-updates |
| Alternative | Self-contained single-file | No runtime dependency, single .exe for USB deployment |

## What NOT to Use

| Avoid | Reason |
|-------|--------|
| EF Core for SQLite | Unnecessary overhead for single-user app. Adds 10-15MB, slower cold starts, migration complexity |
| Prism / Caliburn.Micro | Legacy MVVM frameworks. CommunityToolkit.Mvvm is the modern standard |
| SQL Server / LocalDB | Overkill for single-user app. Adds installation burden |
| Electron | 150MB+ footprint vs 30MB for WPF. Worse performance for data-entry heavy app |
| WinUI 3 | Still maturing, smaller ecosystem. WPF is battle-tested for LOB apps |
| xunit v2 | Deprecated, no longer maintained. Must use v3 |

## Architecture Notes

- **Pattern:** MVVM with Dependency Injection via Generic Host
- **Data access:** Dapper over Microsoft.Data.Sqlite, WAL mode enabled for concurrent read/write
- **Cross-VM communication:** `WeakReferenceMessenger` from CommunityToolkit.Mvvm
- **UI styling:** Built-in Windows 11 Fluent theme + custom DataTemplates for document lists
- **Database location:** `%LocalAppData%\FileTracker\filetracker.db`
- **Attachment storage:** Filesystem directory (not database BLOBs) — `%LocalAppData%\FileTracker\attachments\`
