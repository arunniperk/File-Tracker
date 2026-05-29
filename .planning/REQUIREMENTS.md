# Requirements: File Tracker

**Defined:** 2026-05-29
**Core Value:** Registrar staff can digitally log every document entering or leaving the office, track its current location in the officer hierarchy, attach scanned copies, and generate monthly summary reports.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Document Registration

- [x] **REG-01**: User can register an incoming document with: sender/from, subject, date received, file number (auto-generated), department, priority (Normal/Urgent/Confidential), document type (Letter/Memo/Notice/File/Other), and remarks
- [x] **REG-02**: User can register an outgoing document with: recipient/to, subject, date sent, file number, department, document type, and remarks
- [x] **REG-03**: File numbers are auto-generated in a configurable format (e.g., IITDH/REG/2026/0001)
- [x] **REG-04**: User can edit a registered document's metadata after entry
- [x] **REG-05**: All edits to document records create audit trail entries (who changed what, when)

### Document Search & View

- [x] **SRCH-01**: User can search documents by file number, subject, sender/recipient, or date range
- [x] **SRCH-02**: User can view full details of any registered document in a read-only detail panel
- [ ] **SRCH-03**: User can view the complete movement history of a document (every officer it passed through, with dates)
- [x] **SRCH-04**: Search results are paginated (not all loaded at once)

### Movement & Status Tracking

- [ ] **MOVE-01**: User can record a document movement to an officer (select from configurable hierarchy: Faculty/Department, Registrar, Dean Admin, Director, plus AR, DR, AEE, EE)
- [ ] **MOVE-02**: Each movement records the officer, date, direction (sent/received), and optional remarks
- [ ] **MOVE-03**: User can view the current status/location of any document at a glance
- [ ] **MOVE-04**: Movement history is append-only and immutable (cannot edit or delete movements)
- [x] **MOVE-05**: Officer hierarchy is configurable from the database (add/remove/rename positions)

### Monthly Reports

- [ ] **RPT-01**: User can generate a monthly summary report showing all incoming and outgoing documents for a selected month/year
- [ ] **RPT-02**: Report shows document count by type, by department, and by priority for the period
- [ ] **RPT-03**: User can export the report as PDF
- [ ] **RPT-04**: User can export document data as Excel for further processing

### Document Attachments

- [ ] **ATCH-01**: User can attach scanned document files (PDF, JPG, PNG) to any registered document
- [ ] **ATCH-02**: User can view attached files by opening them in the default system viewer
- [ ] **ATCH-03**: Attachments are stored on the local filesystem (not in the database), organized by document

### Dashboard

- [ ] **DASH-01**: Dashboard shows count of documents pending at each officer
- [ ] **DASH-02**: Dashboard shows recently registered documents (last 7 days)
- [ ] **DASH-03**: Dashboard highlights overdue documents (pending beyond configurable threshold, default 7 days)

### Data Management

- [ ] **DATA-01**: User can backup the database and attachments to a chosen location
- [ ] **DATA-02**: User can restore from a backup file
- [ ] **DATA-03**: Application auto-creates daily backup on close (configurable)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Advanced Features

- **ADV-01**: Barcode/QR code label printing for physical file tracking
- **ADV-02**: Email notifications when documents are overdue
- **ADV-03**: Custom report builder (user-defined columns and filters)
- **ADV-04**: Bulk import of legacy documents from Excel/CSV
- **ADV-05**: Direct scanner integration (WIA/TWAIN) for one-click scan-and-attach

## Out of Scope

| Feature | Reason |
|---------|--------|
| Multi-user login / authentication | Single-user desktop app for one Registrar desk |
| Network / cloud sync | Local-only tool, no server infrastructure |
| Full-text OCR on scanned documents | Adds significant complexity, not core to tracking workflow |
| Mobile app | Desktop-only requirement |
| Workflow approval chains | Registrar tracks movement only, does not enforce approval workflows |
| Email notifications | Added complexity for v1; Registrar monitors from dashboard |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| REG-01 | Phase 1 | Complete |
| REG-02 | Phase 1 | Complete |
| REG-03 | Phase 1 | Complete |
| REG-04 | Phase 1 | Complete |
| REG-05 | Phase 1 | Complete |
| SRCH-01 | Phase 2 | Complete |
| SRCH-02 | Phase 2 | Complete |
| SRCH-03 | Phase 2 | Pending |
| SRCH-04 | Phase 2 | Complete |
| MOVE-01 | Phase 2 | Pending |
| MOVE-02 | Phase 2 | Pending |
| MOVE-03 | Phase 2 | Pending |
| MOVE-04 | Phase 2 | Pending |
| MOVE-05 | Phase 2 | Complete |
| RPT-01 | Phase 3 | Pending |
| RPT-02 | Phase 3 | Pending |
| RPT-03 | Phase 3 | Pending |
| RPT-04 | Phase 3 | Pending |
| ATCH-01 | Phase 3 | Pending |
| ATCH-02 | Phase 3 | Pending |
| ATCH-03 | Phase 3 | Pending |
| DASH-01 | Phase 3 | Pending |
| DASH-02 | Phase 3 | Pending |
| DASH-03 | Phase 3 | Pending |
| DATA-01 | Phase 4 | Pending |
| DATA-02 | Phase 4 | Pending |
| DATA-03 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 27 total
- Mapped to phases: 27
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-29*
*Last updated: 2026-05-29 after initial definition*
