<!-- GSD:project-start source:PROJECT.md -->
## Project

**File Tracker**

A Windows 11 desktop application for the IIT Dharwad Registrar Office to register, track, and report on all incoming and outgoing paper documents. A single-user WPF/.NET desktop tool that replaces manual paper registers with digital entry, scanning, and monthly reporting.

**Core Value:** Registrar staff can digitally log every document entering or leaving the office, track its current location in the officer hierarchy, attach scanned copies, and generate monthly summary reports -- eliminating paper registers and manual follow-ups.

### Constraints

- **Platform**: Windows 11 only
- **Tech stack**: WPF (.NET) + SQLite
- **Users**: Single user (Registrar office staff)
- **Database**: Local file-based SQLite, no server setup required
- **Storage**: Scanned documents stored locally on the machine
<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->
## Technology Stack

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
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
