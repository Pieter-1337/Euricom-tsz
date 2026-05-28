# Timesheet Zone (Tsz)

A consulting-services timesheet platform. Consultants (Users) book time against client Contracts and consume per-year leave allowances; admins approve weekly submissions and review monthly summaries.

## Language

### Identity & customers

**User**:
A person who logs into Tsz. May be a consultant, an admin, or a client manager — these are role assignments on the same entity, not separate types.
_Avoid_: Employee, Account, Person

**Customer**:
An organisation that Tsz invoices for consulting work.
_Avoid_: Client, Account, Tenant

### Impersonation

**Impersonation**:
A session in which an Admin assumes another User's identity, able to see and do **exactly** what that User could — no more (Admin-only powers do not carry over) and no less.
_Avoid_: Sudo, Switch-user, Login-as

**Impersonator**:
The Admin User who has assumed another User's identity. Identified by the real authenticated identity behind the request, independent of who is being impersonated.
_Avoid_: Actor, Operator

**Impersonated User**:
The target User whose identity an Impersonator has assumed for the session.
_Avoid_: Victim, Subject, Target

### Contracts

**Contract**:
A date-bounded agreement between a Customer and one or more consultant Users, containing one or more ContractTasks.
_Avoid_: Engagement, Project, SoW

**ContractTask**:
A billable line item on a Contract; carries a rate. Work hours are booked against ContractTasks.
_Avoid_: WorkItem, Activity, Line

### Bookings

**TimeEntry**:
A single work-hour booking by one User against one ContractTask on one date. Child entity of a TimesheetWeek.
_Avoid_: WorkLog, Hours, Entry

**LeaveBooking**:
A single leave-hour booking by one User against one LeaveType on one date. Child entity of a TimesheetWeek. Counts against the User's UserLeave allowance for that LeaveType and year.
_Avoid_: TimeOff, Absence, Vacation, LeaveEntry

**TimesheetWeek**:
The per-User × per-ISO-week aggregate root. Owns its TimeEntries and LeaveBookings and carries the week's Status (Draft → Submitted → Approved). The week — not the individual booking — is the unit of approval.
_Avoid_: WorkWeek, WeekSheet, WeeklyTimesheet

**Timesheet**:
The month-level read view shown in the FE. NOT a persisted entity — produced by a query handler aggregating bookings across the relevant TimesheetWeeks for a User × month.
_Avoid_: MonthSheet, MonthlyReport

**Leave Overview**:
The year-level read view shown in the FE for one User — calendar grid of LeaveBookings + public Holidays, plus a balance panel of UserLeave allowances. NOT a persisted entity AND NOT a backend module — composed FE-side from three independent endpoints owned by `LeaveTypes` (allowance), `Timesheets` (bookings), and `Workdays` (holidays). Sibling to Timesheet in being a view-only domain term.
_Avoid_: LeaveCalendar, LeaveDashboard, YearSheet

### Admin surfaces

**My Tasks**:
An admin-facing FE to-do view listing actions an admin needs to take. NOT a persisted entity and NOT a backend module — it is a read view over work that needs doing. v1 contains exactly one task type: **Approval Tasks** (TimesheetWeeks in `Submitted` status awaiting an approve/reopen decision). Intentionally hardcoded to approvals — no generic task table or task-type infrastructure until a second task type actually arrives.
_Avoid_: Inbox, Approvals (as the page name), Todos. Note "Tasks" here is the admin's to-do items and is distinct from **ContractTask** (a billable contract line).

**Approval Task**:
One row in **My Tasks**: a single `Submitted` **TimesheetWeek** that an admin can Approve or Reopen. Derived live from week Status; not stored.

### Leave catalog

**LeaveType**:
A catalogued kind of leave (Verlof, ADV, Feestdag, …). Reference data managed by admins. Lives in its own module (`Tsz.Modules.LeaveTypes`).
_Avoid_: LeaveCategory, AbsenceType

**UserLeave**:
Per-User × per-year allowance for a specific LeaveType ("Pieter has 20 Verlof days in 2026"). Edited via the User admin form. Stored in the LeaveTypes module (extracted from Users per ADR-0002). `TotalDays = null` means "unlimited" for v1.
_Avoid_: LeaveBudget, LeaveBalance, LeaveQuota

**WorkdayCapacity**:
A consultant's daily working hours. v1 constant: `8.00` for everyone. Drives both the per-day booking cap ("Σ TimeEntry + LeaveBooking DurationHours per (User, Date) ≤ WorkdayCapacity") and the leave-day-equivalence arithmetic ("1 day deducted from a UserLeave allowance = WorkdayCapacity hours"). Forward-compat: becomes `User.WorkingPatternHoursPerDay` when part-time consultants land.
_Avoid_: DefaultDayHours (implementation name), DailyBudget, DayTotalCap, MaxWorkdayHours — these describe only one use of the number and hide the connection.

## Relationships

- A **Contract** has many **ContractTask**s and is assigned to one or more **User**s (consultants)
- A **User** has zero or more **UserLeave** rows (one per LeaveType per year)
- A **TimesheetWeek** belongs to exactly one **User** and one ISO week
- A **TimesheetWeek** contains zero or more **TimeEntry** children and zero or more **LeaveBooking** children
- A **TimeEntry** references exactly one **ContractTask**
- A **LeaveBooking** references exactly one **LeaveType** and counts toward a **UserLeave** allowance
- A **Timesheet** (month view) is a read-only projection over the **TimesheetWeek**s of a User × month

## Flagged ambiguities

- *"time entry" vs "timesheet"* — colloquially overlap. Resolution: **TimeEntry** is one row of booked work; **TimesheetWeek** is the persisted week-level container with a Status; **Timesheet** is the month-level *view* (not persisted). Never persist a "Timesheet" row.
- *"WeekApproval"* — there is no separate WeekApproval entity. Approval state is a `Status` field on **TimesheetWeek**.
- *"who approves?"* — v1: **Admin** only. Approve/Reopen are `RequireAdmin`. ClientManager approval is **deferred**: it needs a ClientManager→Customer ownership model (which consultants' weeks a CM may approve) that does not exist yet — `RequireAdminOrAnyClientManager` is unscoped and `DataScopeAccessor` only does owner-equals for non-admins. Revisit when CM scoping lands.
- *"can an admin edit another User's bookings directly?"* — No. An admin acting on another User's **TimesheetWeek** may **View, Approve, and Reopen** only (week-Status powers). Editing TimeEntries/LeaveBookings stays self-only (booking writes are `caller == userId`). The blessed way for an admin to correct someone's entries is **Impersonation** (see ADR-0004), which provides the full edit→Submit loop with the documented attribution rules. Reason: direct admin-edit would duplicate Impersonation and produces a stuck-in-Draft half-flow (admin can edit a reopened Draft but cannot Submit or Approve it).
- *"leave"* alone is ambiguous (catalog vs allowance vs consumption). Use **LeaveType** (catalog), **UserLeave** (allowance), **LeaveBooking** (consumption).
- *"who did this?"* under **Impersonation** — v1 decision: actions an **Impersonator** performs are attributed **solely** to the **Impersonated User** with no persisted trace of the Impersonator, including the `TimesheetWeek` Status transitions (Submit/Approve/Reopen). Capability-transparency was chosen over attestation-traceability. Revisit if accountability requirements arrive.
- *"Feestdag"* — refers to a public **Workdays.Holiday**, NOT a LeaveType. The seed has no `Feestdag` LeaveType (Verlof, ADV dagen, Anciënniteit, Ziekte only). The "Feestdagen" row in the Leave Overview balance panel is FE-synthesized from Holiday counts.
