# Feature Landscape

**Domain:** Government / Educational Registrar Office File Tracking Systems
**Researched:** 2026-05-29
**Project:** IIT Dharwad Registrar Office File Tracker (Windows 11 WPF Desktop)

---

## Table Stakes

Features users expect. Missing any of these and the product feels incomplete or unusable for its core purpose. These are the minimum viable feature set for a registrar file tracking system.

| # | Feature | Why Expected | Complexity | Notes |
|---|---------|--------------|------------|-------|
| 1 | **Document Registration — Incoming** | The entire system exists to log incoming paper documents. Without this, nothing else matters. | Low | Form-based entry: sender name, sender department/organization, subject, date received, file number (auto or manual), document type, priority level, remarks. |
| 2 | **Document Registration — Outgoing** | Registrar offices both receive AND send documents. Outgoing tracking closes the loop. | Low | Mirror of incoming form with: recipient name, recipient department, subject, date sent, file number reference, dispatch method. |
| 3 | **Auto-generated Unique File Number** | Every government document carries a file number; it's the primary identifier used in all correspondence and lookup. | Low | Configurable format (e.g., `IITDH/REG/YYYY/NNNN`). Must be unique and sequential. Users expect to search by file number above all else. |
| 4 | **Current Status & Location Tracking** | Users must know where any document physically is at any moment in the officer hierarchy. This replaces manual follow-up. | Medium | Shows which officer currently holds the document. Status values: Received, In Transit, With Officer, Dispatched, Closed/Archived. |
| 5 | **Document Movement / Routing History** | Each movement between officers must be logged so users can trace the full journey of a document. | Medium | Timestamped log: from officer → to officer, date/time, action taken (forwarded, returned, approved, noted). Immutable audit trail. |
| 6 | **Search and Filter** | Users need to find documents by file number, sender, subject, date range, or current location. | Medium | Search by: file number (primary), sender name, subject keywords, date range, document type, current officer, priority. Combined filters. |
| 7 | **Document Type Classification** | Registrar offices handle multiple document categories (memos, applications, reports, letters, circulars). | Low | Predefined types: Memo, Letter, Application, Report, Circular, Note Sheet, Tender, Other. Plus an "add custom type" escape hatch. |
| 8 | **Date Tracking (Multiple Dates)** | Every document has a chain of dates: received date, sent date, movement dates. | Low | Receive date, send date (outgoing), last movement date, expected action date. Date picker with calendar control. |
| 9 | **Sender / Source Tracking** | Government documents always identify the originating authority. | Low | Sender name, sender designation, sending department/organization. For outgoing: recipient equivalents. |
| 10 | **Data Persistence & Reliable Storage** | If data is lost, the system is worthless. Single SQLite file must be robust. | Low | SQLite with proper transactions. Automatic backup of the database file on application close. |

---

## Differentiators

Features that set this product apart from a bare-minimum register. Not strictly required for function, but provide significant workflow improvement, reporting value, or user satisfaction.

| # | Feature | Value Proposition | Complexity | Notes |
|---|---------|-------------------|------------|-------|
| 1 | **Scanned Document Attachment** | Replaces physical photocopying. Staff can attach a scanned PDF/JPG of the original document to the digital entry for instant reference. | Medium | Scanner integration (WIA/TWAIN via WPF), store as files on local disk referenced in SQLite. Support PDF and common image formats. |
| 2 | **Officer Hierarchy & Routing Visualization** | Shows the document's path through IIT Dharwad's specific hierarchy (Faculty → Registrar → Dean Admin → Director, with AR, DR, AEE, EE). | High | Configurable officer list with roles, visual movement chain per document. The precise routing chain is domain-specific and a key differentiator vs generic file tracking. |
| 3 | **Monthly Summary Reports** | One-click generation of formatted reports: "All incoming documents for March 2026," "All outgoing for April 2026." Critical for monthly office reviews. | Medium | Filter by date range + direction (in/out). Table format with counts. Print-friendly layout. |
| 4 | **Report Export (PDF / Excel)** | Government offices require printed records and spreadsheet data for audits, RTI responses, and annual reports. | Medium | Export filtered results to PDF (formatted) and Excel (.xlsx) for further analysis. |
| 5 | **Priority / Urgency Flagging** | Not all documents are equal. Flagging urgent documents ensures they surface in the dashboard. | Low | Priority levels: Normal, Urgent, Immediate. Color-coded in the UI. Sortable/filterable column. |
| 6 | **Dashboard / Home View** | At-a-glance summary when the application opens: counts by status, recent entries, pending documents, urgent items. | Medium | Cards showing: total incoming today, total outgoing today, documents requiring action, overdue items, urgent flagged. |
| 7 | **Quick-Add with Keyboard Shortcuts** | Registrar staff perform high-volume data entry. Keyboard-first workflow dramatically improves speed. | Low | Tab-key field navigation, F5 for new incoming, F6 for new outgoing, Ctrl+S to save, type-ahead on sender/department fields. |
| 8 | **Sender / Department Auto-Complete** | Reduces typing errors and speeds up entry for repeat correspondents. | Low | Learns from previously entered senders and departments. Dropdown with fuzzy matching as user types. |
| 9 | **Pending / Overdue Action Tracking** | Documents with an expected action date that pass without action. Critical for accountability. | Medium | "Expected Action By" date field. Dashboard panel for overdue items. Visual highlight (red) for overdue documents. |
| 10 | **Print-Ready Document Slip** | Print a small tracking slip with file number, subject, current officer that can be physically attached to the paper file. | Low | Single-click print of a formatted slip (file number, subject, date, current holder). |
| 11 | **Remark / Notes with Timestamp** | Officers add remarks when forwarding documents. These accumulate alongside movement history. | Low | Free-text remark field per movement entry. Stored with the movement log, not as a separate system. |
| 12 | **Configurable File Number Format** | Different offices use different numbering conventions (IITDH vs department-specific prefixes). | Medium | Admin setting to define: prefix, year inclusion toggle, number padding, department code segment. |
| 13 | **Data Backup & Restore** | Since SQLite is a single file, a one-click backup/restore feature prevents data loss disasters. | Low | "Backup Database" menu item → file save dialog. "Restore from Backup" → file open dialog with confirmation. |
| 14 | **Bulk Status View (List View)** | Spreadsheet-style grid view of all documents with sortable columns. Preferred by power users over form-by-form navigation. | Medium | DataGrid with columns: file number, type, sender, subject, date, current officer, priority, status. Sort and filter per column. |

---

## Anti-Features

Features to explicitly NOT build. These would increase complexity, violate constraints, or add maintenance burden without commensurate value for this specific use case.

| # | Anti-Feature | Why Avoid | What to Do Instead |
|---|-------------|-----------|-------------------|
| 1 | **Multi-User Login / Role-Based Access** | Single-user desktop app by design. Adding auth, sessions, concurrent access adds massive complexity (needs server, conflict resolution). | Keep it single-user with local SQLite. If multi-user is needed in future, a complete architecture rewrite is required — not an incremental feature. |
| 2 | **Email Notifications** | Out of scope per requirements. Would require SMTP config, network access, background services. | Rely on in-app dashboard for pending/overdue visibility. Print slip for physical handoff. |
| 3 | **Barcode / QR Code Scanning** | Requires hardware, image processing library, and label printing infrastructure. Out of scope for v1. | Use auto-generated file numbers as the primary identifier. Can be added later as a phase if needed. |
| 4 | **Cloud Synchronization** | Violates the zero-network, local-only constraint. Introduces security concerns for government documents. | SQLite backup/restore to a file. Manual transfer if needed. |
| 5 | **Workflow Approval Chains** | Registrar documents follow a hierarchy but don't require digital approval workflows (sign-off happens physically on paper). | Track movements through the hierarchy as simple location changes, not as approval states. |
| 6 | **Full-Text OCR Search of Attachments** | Computationally expensive, requires OCR library (Tesseract), slow on large scans, adds significant complexity. | Attachments serve as reference copies. Search relies on metadata fields (file number, sender, subject, date). |
| 7 | **Document Versioning / Revision Tracking** | Registrar office deals with physical paper documents that don't have versions. The digital entry is a log, not a living document. | Each registration is immutable once saved. Corrections must be made via a new remark, not by editing history. |
| 8 | **Mobile App / Remote Access** | Single-user desktop on a specific Windows 11 machine. No remote access requirement. | Not applicable to v1. |
| 9 | **Integration with External Systems (ERP, Email, CMS)** | Adds dependency management, API complexity, network requirements. No integration requirements identified. | Keep it self-contained. Export to PDF/Excel for integration via manual import into other systems. |
| 10 | **Advanced Analytics / Charts / Dashboards** | Registrar office needs simple counts and lists, not data visualization. Charts add UI complexity without operational value. | Summary counts on the dashboard. Monthly report is a table, not a chart. |
| 11 | **Audit Log / User Activity Tracking** | Single-user system. "Who did what" is not a question when only one person operates the system. | Movement history serves as the audit trail for document routing. |
| 12 | **Document Retention Schedule / Auto-Archival** | Adds legal compliance complexity. Physical document retention is governed by institutional policy, not the tracking tool. | Manual "Archive" status toggle. Reports can filter by archived status. |
| 13 | **Template-Based Document Generation** | The system tracks existing documents; it doesn't create new ones. Document generation would be a separate product category. | Not applicable. |

---

## Feature Dependencies

Some features depend on others being built first. This maps the build order constraints.

```
Document Registration (In/Out)
  ├── Auto-generated File Number
  ├── Document Type Classification
  ├── Sender/Source Tracking
  ├── Date Tracking
  └── Priority Flagging
       │
       ▼
Search & Filter (needs registration data)
       │
       ▼
Document Movement / Routing History
  ├── Officer Hierarchy Configuration
  ├── Current Status & Location Tracking
  └── Remarks with Timestamp
       │
       ▼
Dashboard / Home View (needs data + movement data)
       │
       ▼
Monthly Summary Reports → Report Export (PDF/Excel)
       │
       ▼
Scanned Document Attachment (independent of reporting)
       │
       ▼
Print-Ready Document Slip (independent of scanning)
```

**Key dependency chains:**
- **Registration → Search → Movement → Dashboard → Reports**: This is the core spine.
- **Scanned attachments** and **print slips** are leaf features — they can be added any time after registration exists.
- **Quick-add / auto-complete / keyboard shortcuts** are UX polish layers applied on top of existing forms.
- **Configurable file number format** must be designed into registration from the start, even if the UI for configuring it comes later.

---

## MVP Recommendation

For the first releasable version (Milestone 1), prioritize:

### Phase 1: Core Registration & Lookup
1. **Document Registration — Incoming** (Table Stake #1)
2. **Document Registration — Outgoing** (Table Stake #2)
3. **Auto-generated Unique File Number** (Table Stake #3)
4. **Document Type Classification** (Table Stake #7)
5. **Date Tracking** (Table Stake #8)
6. **Sender / Source Tracking** (Table Stake #9)
7. **Search and Filter** (Table Stake #6)
8. **Data Persistence** (Table Stake #10)

### Phase 2: Movement & Status Tracking
9. **Officer Hierarchy Configuration** (Differentiator #2 — data model)
10. **Document Movement / Routing History** (Table Stake #5)
11. **Current Status & Location Tracking** (Table Stake #4)
12. **Priority / Urgency Flagging** (Differentiator #5)
13. **Remarks with Timestamp** (Differentiator #11)

### Phase 3: Reporting & Attachments
14. **Dashboard / Home View** (Differentiator #6)
15. **Monthly Summary Reports** (Differentiator #3)
16. **Report Export — PDF / Excel** (Differentiator #4)
17. **Scanned Document Attachment** (Differentiator #1)
18. **Print-Ready Document Slip** (Differentiator #10)

### Phase 4: Polish & Power User
19. **Sender / Department Auto-Complete** (Differentiator #8)
20. **Quick-Add Keyboard Shortcuts** (Differentiator #7)
21. **Pending / Overdue Action Tracking** (Differentiator #9)
22. **Configurable File Number Format** (Differentiator #12)
23. **Data Backup & Restore** (Differentiator #13)

**Defer beyond v1:**
- Bulk status view (list/grid) — can be added as an alternate view if user feedback demands it
- Advanced report customization (date range slicers, grouped summaries)

---

## Complexity Scale Reference

| Complexity | Meaning | Typical Effort (Single Dev) |
|-----------|---------|---------------------------|
| **Low** | Simple form, single table, standard WPF controls. No algorithm. | 0.5–1 day |
| **Medium** | Multi-table operations, custom UI element, data transformation, I/O operations. | 1–3 days |
| **High** | External device integration, significant custom logic, performance considerations, multiple subsystems. | 3–7 days |

---

## Sources

| Source | Confidence | Notes |
|--------|-----------|-------|
| Wikipedia: Records Management (ISO 15489 lifecycle) | HIGH | Authoritative reference on records management domains: capture, classification, storage, retrieval, circulation, disposition |
| Wikipedia: Document Management System components | HIGH | Canonical DMS components: metadata, capture, indexing, storage, retrieval, security, workflow, versioning, searching |
| PROJECT.md (IIT Dharwad requirements) | HIGH | Primary source — validated project requirements from stakeholder interview |
| project.json (user chat history) | HIGH | Original conversation establishing hierarchy: Faculty → Registrar → Dean Admin → Director with AR, DR, AEE, EE intermediate officers |
| Domain knowledge: Indian government file tracking conventions (eOffice, CPGRAMS, institutional registrar offices) | MEDIUM | Inferred from training data and common patterns in Indian government/educational file management; not verified against official eOffice documentation (docs.nic.in inaccessible) |
| IJERT/IRJET academic papers on registrar file tracking systems | LOW | Paywalled/unfetchable; referenced only for domain pattern confirmation |

**Overall confidence:** MEDIUM-HIGH
- Table stakes features: HIGH confidence (consistent across records management standards and project requirements)
- Differentiators: MEDIUM-HIGH confidence (derived from specific IIT Dharwad requirements + standard registrar office workflows)
- Anti-features: HIGH confidence (explicitly documented in PROJECT.md "Out of Scope" plus sound architectural judgment for single-user desktop constraints)
