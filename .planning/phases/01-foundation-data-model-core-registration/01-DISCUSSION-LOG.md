# Phase 1: Foundation — Data Model & Core Registration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 01-foundation-data-model-core-registration
**Areas discussed:** File Numbers, UI Layout, Audit Trail, Save Behavior

---

## File Numbers

### Format Decision
| Option | Description | Selected |
|--------|-------------|----------|
| IITDH/REG/YYYY/NNNN | Institutional prefix, auto-year, auto-increment | |
| Custom prefix + year + serial | Simpler format with configurable prefix | |
| I'll provide the format | Describe the exact format used at IIT Dharwad | |

**User's choice:** IITDH/REG/YYYY/NNNN (Recommended) — but then revised after follow-up question

### Original vs Generated
| Option | Description | Selected |
|--------|-------------|----------|
| Manual entry | User types the file number for every document | |
| User enters original + auto tracking ID | Manual entry of original, system generates internal tracking ID | ✓ |

**User's choice:** User enters original file number from document, system auto-generates internal tracking ID

### Tracking ID Format
| Option | Description | Selected |
|--------|-------------|----------|
| Sl.No/YYYY | Simple serial: 0001/2026, resets yearly | ✓ |
| UUID behind scenes | GUID internally, not shown to user | |

**User's choice:** Sl.No/YYYY format — simple, verbally referenceable

---

## UI Layout

### Form Structure
| Option | Description | Selected |
|--------|-------------|----------|
| Two separate forms | Incoming and Outgoing as distinct forms | |
| Single form with toggle | One form, Incoming/Outgoing radio toggle changes fields | ✓ |
| Tabbed interface | Two tabs: Incoming \| Outgoing | |

**User's choice:** Single form with toggle

### MVP Fields
| Option | Description | Selected |
|--------|-------------|----------|
| Full fields | Sender/Recipient, Subject, Date, File No, Department, Priority, Type, Remarks | |
| Simplified | Sender/Recipient, Subject, Date, File Number, Remarks only | ✓ |

**User's choice:** Simplify to common fields for Phase 1 — drop Department/Priority/Type

---

## Audit Trail

### Display Style
| Option | Description | Selected |
|--------|-------------|----------|
| Simple log list | Table: timestamp, field, old value, new value | ✓ |
| Expansion panels | Click to expand each edit entry | |
| Inline below fields | Edit history beneath each field on detail view | |

**User's choice:** Simple log list — chronological, newest first

---

## Save Behavior

### Save Strategy
| Option | Description | Selected |
|--------|-------------|----------|
| Explicit Save button | User fills form, clicks Save. Warn on close with unsaved changes | ✓ |
| Auto-save on field change | Saves as user types — risk of partial entries | |

**User's choice:** Explicit Save button with unsaved-changes warning

---

## Claude's Discretion

- Database schema design (tables, columns, indexes, PRAGMA configuration)
- WPF MVVM project structure and file organization
- Exact form styling, spacing, typography
- Error handling and validation patterns
- DI container setup and service registration
- SQLite connection string and WAL mode configuration

## Deferred Ideas

- Department, Priority, Document Type fields → Phase 2 (Search & Movement Tracking)
- Configurable file number format (IITDH/REG/YYYY/NNNN) → deferred, Phase 1 uses Sl.No/YYYY
