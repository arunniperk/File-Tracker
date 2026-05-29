# File Tracker

## What This Is

A Windows 11 desktop application for the IIT Dharwad Registrar Office to register, track, and report on all incoming and outgoing paper documents. A single-user WPF/.NET desktop tool that replaces manual paper registers with digital entry, scanning, and monthly reporting.

## Core Value

Registrar staff can digitally log every document entering or leaving the office, track its current location in the officer hierarchy, attach scanned copies, and generate monthly summary reports -- eliminating paper registers and manual follow-ups.

## Requirements

### Validated

(None yet -- ship to validate)

### Active

- [ ] Register incoming documents with full metadata (sender, subject, file number, department, priority, type, remarks, date)
- [ ] Register outgoing documents with full metadata
- [ ] Attach scanned copies of documents to entries
- [ ] Track document movement through officer hierarchy (Faculty/Departments -> Registrar -> Dean Admin -> Director, with AR, DR, AEE, EE)
- [ ] View current status and location of any document
- [ ] Generate monthly summary reports (all incoming/outgoing for a given period)
- [ ] Search and filter registered documents

### Out of Scope

- Multi-user or network-based access -- single-user desktop only
- Email notifications -- v1 focuses on in-app tracking
- Barcode/QR code scanning for physical file tracking

## Context

- **Organization**: IIT Dharwad (iitdh.ac.in) Registrar Office
- **Document flow hierarchy**: Faculty/Departments -> Registrar -> Dean Admin -> Director, with intermediate officers: Assistant Registrar, Deputy Registrar, Assistant Executive Engineer, Executive Engineer
- **Current process**: Manual paper registers, no digital tracking
- **Environment**: Windows 11, single-user operation at the Registrar's desk

## Constraints

- **Platform**: Windows 11 only
- **Tech stack**: WPF (.NET) + SQLite
- **Users**: Single user (Registrar office staff)
- **Database**: Local file-based SQLite, no server setup required
- **Storage**: Scanned documents stored locally on the machine

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| WPF over Electron/Web | Native Windows performance, excellent data-entry UX, smaller footprint | -- Pending |
| SQLite over SQL Server | Zero-install, file-based, perfect for single-user desktop | -- Pending |
| Single-user vs multi-user | Registrar office operates from one desk | -- Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? -> Move to Out of Scope with reason
2. Requirements validated? -> Move to Validated with phase reference
3. New requirements emerged? -> Add to Active
4. Decisions to log? -> Add to Key Decisions
5. "What This Is" still accurate? -> Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check -- still the right priority?
3. Audit Out of Scope -- reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-05-29 after initialization*
