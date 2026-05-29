# Roadmap: File Tracker

## Overview

A 4-phase build delivering an IIT Dharwad Registrar Office file tracking system. Each phase delivers end-to-end usable functionality: registration → search & movement tracking → dashboard/reports/attachments → data safety. Phases follow the natural dependency chain where registration data feeds search, search enables movement, movement data populates the dashboard, and reports consume all accumulated data.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3, 4): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation — Data Model & Core Registration** - SQLite database, MVVM architecture, and incoming/outgoing document registration with auto-generated file numbers and audit trails
- [ ] **Phase 2: Search & Movement Tracking** - Document search with pagination, configurable officer hierarchy, and append-only movement tracking with current status
- [ ] **Phase 3: Dashboard, Reports & Attachments** - Operational dashboard with pending/overdue tracking, monthly summary reports with PDF/Excel export, and scanned document attachments
- [ ] **Phase 4: Data Safety & Management** - One-click backup/restore, automatic daily backups on close, and database integrity checks on startup

## Phase Details

### Phase 1: Foundation — Data Model & Core Registration
**Goal**: Registrar staff can digitally register all incoming and outgoing documents with auto-generated file numbers, edit metadata with full audit trail, and trust that data is safely persisted across restarts.
**Mode**: mvp
**Depends on**: Nothing (first phase)
**Requirements**: REG-01, REG-02, REG-03, REG-04, REG-05
**Success Criteria** (what must be TRUE):
  1. User can register an incoming document with all required fields (sender, subject, file number, department, priority, type, remarks, date) and save it to the database
  2. User can register an outgoing document with all required fields (recipient, subject, file number, department, type, remarks, date) and save it to the database
  3. File numbers auto-generate in the configured format (e.g., IITDH/REG/2026/0001), are guaranteed unique, and cannot be blank or duplicated
  4. User can edit a previously registered document's metadata and see that the change is recorded in an immutable audit trail (who changed what, when)
  5. Application starts, creates/reopens the database, and all registered documents persist across restarts without data loss
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md — Walking skeleton: project scaffold, DI host, SQLite database, incoming document registration form
- [ ] 01-02-PLAN.md — Tracking ID generation (Sl.No/YYYY), outgoing registration toggle, form validation, unsaved changes warning
- [ ] 01-03-PLAN.md — Document edit with field-level diff, immutable audit trail, document detail panel with audit history display

### Phase 2: Search & Movement Tracking
**Goal**: Staff can find any document, view its full history, record movements through the configurable officer hierarchy, and know exactly where every document is at any moment.
**Depends on**: Phase 1
**Requirements**: SRCH-01, SRCH-02, SRCH-03, SRCH-04, MOVE-01, MOVE-02, MOVE-03, MOVE-04, MOVE-05
**Success Criteria** (what must be TRUE):
  1. User can search documents by file number, subject, sender/recipient, or date range and see paginated results
  2. User can view full details of any document including all metadata in a read-only detail panel
  3. User can view the complete movement history of any document — every officer it passed through, with dates, direction, and remarks
  4. User can record a document movement to any officer from the configurable hierarchy with direction (sent/received) and optional remarks
  5. User can see the current location of any document at a glance, the movement history is append-only and cannot be edited or deleted, and the officer hierarchy can be configured (positions added, renamed, reordered) through the UI
**Plans**: TBD
**UI hint**: yes

### Phase 3: Dashboard, Reports & Attachments
**Goal**: Staff have an at-a-glance operational dashboard, can generate and export monthly reports for audits, and can attach scanned copies to document records.
**Depends on**: Phase 2
**Requirements**: DASH-01, DASH-02, DASH-03, RPT-01, RPT-02, RPT-03, RPT-04, ATCH-01, ATCH-02, ATCH-03
**Success Criteria** (what must be TRUE):
  1. User sees a dashboard showing documents pending at each officer, recently registered documents (last 7 days), and overdue documents highlighted in red
  2. User can generate a monthly summary report showing all incoming and outgoing documents for a selected month/year
  3. Report includes breakdowns by document type, by department, and by priority for the selected period
  4. User can export the report as a formatted PDF and export document data as Excel for further processing
  5. User can attach scanned document files (PDF, JPG, PNG) to any registered document and open them in the default system viewer
**Plans**: TBD
**UI hint**: yes

### Phase 4: Data Safety & Management
**Goal**: Staff trust their data is safe with one-click backup and restore, automatic daily backups, and startup integrity verification.
**Depends on**: Phase 3
**Requirements**: DATA-01, DATA-02, DATA-03
**Success Criteria** (what must be TRUE):
  1. User can create a backup of the entire database and all attachments to any chosen folder location
  2. User can restore the system from a previously created backup file with a confirmation prompt
  3. Application automatically creates a timestamped database backup on close (when configured) and runs an integrity check on startup to detect corruption
**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation — Data Model & Core Registration | 1/3 | In Progress|  |
| 2. Search & Movement Tracking | 0/TBD | Not started | - |
| 3. Dashboard, Reports & Attachments | 0/TBD | Not started | - |
| 4. Data Safety & Management | 0/TBD | Not started | - |
