# File Tracker

A Windows desktop application for the **IIT Dharwad Registrar Office** to digitally track all incoming and outgoing paper documents.

## Features

- **Document Registration** — Register incoming and outgoing documents with original file numbers, auto-generated tracking IDs, and full metadata
- **Audit Trail** — Every edit is recorded with field-level diff (timestamp, old value, new value) — immutable and append-only
- **Search & Filter** — Find documents by file number, tracking ID, subject, sender/recipient, or date range with paginated results
- **Movement Tracking** — Record document movements through the configurable officer hierarchy (Faculty → Registrar → Dean Admin → Director)
- **Monthly Reports** — Generate and export monthly summaries as PDF and Excel
- **Document Attachments** — Attach scanned copies (PDF, JPG, PNG) to document records
- **Backup & Restore** — One-click database and attachment backup with automatic daily backups

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 9 (Windows) |
| UI | WPF with Windows 11 Fluent theme |
| Architecture | MVVM + Layered Architecture with DI |
| Database | SQLite (WAL mode, PRAGMA foreign_keys = ON) |
| Data Access | Dapper + Microsoft.Data.Sqlite |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| Logging | Serilog |
| Testing | xunit.v3 (46 tests) |

## Project Structure

```
FileTracker/
├── FileTracker.sln
├── src/
│   ├── FileTracker.App/       # WPF Views, ViewModels, App startup
│   ├── FileTracker.Core/      # Domain models, DTOs, Services, Interfaces
│   └── FileTracker.Data/      # Repositories, DatabaseInitializer, SQL
├── tests/
│   └── FileTracker.Tests/     # Unit tests (xunit.v3)
└── .planning/                 # GSD project planning artifacts
    ├── PROJECT.md
    ├── REQUIREMENTS.md
    ├── ROADMAP.md
    ├── research/
    └── phases/
```

## Getting Started

### Prerequisites

- Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build & Run

```bash
dotnet build
dotnet run --project src/FileTracker.App
```

### Run Tests

```bash
dotnet test
```

## Database

The application uses SQLite with the database stored at:

```
%LocalAppData%\FileTracker\filetracker.db
```

Attachments are stored in:

```
%LocalAppData%\FileTracker\attachments\
```

## Document Hierarchy

The officer hierarchy mirrors the IIT Dharwad organizational structure:

Faculty/Departments → Assistant Registrar → Deputy Registrar → Registrar → Dean Admin → Director

With intermediate positions: Assistant Executive Engineer, Executive Engineer

## Developer

Arun Verma, Assistant Registrar, IIT Dharwad

## License

Internal use — IIT Dharwad Registrar Office.
