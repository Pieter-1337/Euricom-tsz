// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from 'vite-plus/test';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import type { TimesheetWeek, SelectableContractTask, SelectableLeaveType } from '#/api/timesheets';

const { submitTimesheetBookingsMock } = vi.hoisted(() => ({
  submitTimesheetBookingsMock: vi.fn(),
}));

vi.mock('#/features/timesheets/server-fns', () => ({
  submitTimesheetBookings: submitTimesheetBookingsMock,
  submitWeekLifecycle: vi.fn(),
  approveWeekLifecycle: vi.fn(),
  reopenWeekLifecycle: vi.fn(),
}));

vi.mock('@tanstack/react-router', () => ({
  useNavigate: () => vi.fn(),
  useRouter: () => ({ invalidate: vi.fn() }),
  useBlocker: () => undefined,
  Link: ({ children }: { children: React.ReactNode }) => children,
}));

import { TimesheetWeekGrid } from './timesheet-week-grid';

const USER_ID = '00000000-0000-0000-0000-000000000001';
const TASK_A = '00000000-0000-0000-0000-000000000010';
const TASK_B = '00000000-0000-0000-0000-000000000011';
const LEAVE_A = '00000000-0000-0000-0000-000000000020';

const days = [
  { date: '2026-05-18', isBusinessDay: true }, // Mon
  { date: '2026-05-19', isBusinessDay: true }, // Tue
  { date: '2026-05-20', isBusinessDay: true }, // Wed
  { date: '2026-05-21', isBusinessDay: true }, // Thu
  { date: '2026-05-22', isBusinessDay: true }, // Fri
  { date: '2026-05-23', isBusinessDay: false }, // Sat
  { date: '2026-05-24', isBusinessDay: false }, // Sun
];

function makeInitialData(overrides?: Partial<TimesheetWeek>): TimesheetWeek {
  return {
    id: null,
    userId: USER_ID,
    isoYear: 2026,
    isoWeek: 21,
    status: 'Draft',
    days,
    timeEntries: [],
    leaveBookings: [],
    ...overrides,
  } as unknown as TimesheetWeek;
}

const selectableTasks: SelectableContractTask[] = [
  { contractTaskId: TASK_A, taskName: 'Task A', contractId: 'c1', contractSubject: 'Contract A', customerId: 'cust1' },
  { contractTaskId: TASK_B, taskName: 'Task B', contractId: 'c1', contractSubject: 'Contract A', customerId: 'cust1' },
] as unknown as SelectableContractTask[];

const selectableLeaveTypes: SelectableLeaveType[] = [
  { id: LEAVE_A, name: 'Verlof' },
] as unknown as SelectableLeaveType[];

describe('TimesheetWeekGrid — day capacity', () => {
  beforeEach(() => {
    submitTimesheetBookingsMock.mockReset();
    cleanup();
  });

  it('shows the red "{total}h / 8h max" indicator when a day total exceeds the cap', () => {
    // Pre-populate Monday: TaskA = 5h, TaskB = 4h → 9h, over cap.
    const initial = makeInitialData({
      timeEntries: [
        { contractTaskId: TASK_A, date: '2026-05-18', durationHours: 5, taskName: 'Task A' },
        { contractTaskId: TASK_B, date: '2026-05-18', durationHours: 4, taskName: 'Task B' },
      ],
    } as Partial<TimesheetWeek>);

    render(
      <TimesheetWeekGrid
        userId={USER_ID}
        year={2026}
        week={21}
        initialData={initial}
        selectableTasks={selectableTasks}
        selectableLeaveTypes={selectableLeaveTypes}
        isAdmin={false}
      />,
    );

    const cell = screen.getByTestId('day-total-2026-05-18');
    expect(cell.textContent).toBe('9h / 8h max');
    expect(cell.className).toContain('text-red-600');
  });

  it('renders day totals normally when under the cap', () => {
    const initial = makeInitialData({
      timeEntries: [
        { contractTaskId: TASK_A, date: '2026-05-18', durationHours: 4, taskName: 'Task A' },
        { contractTaskId: TASK_B, date: '2026-05-18', durationHours: 4, taskName: 'Task B' },
      ],
    } as Partial<TimesheetWeek>);

    render(
      <TimesheetWeekGrid
        userId={USER_ID}
        year={2026}
        week={21}
        initialData={initial}
        selectableTasks={selectableTasks}
        selectableLeaveTypes={selectableLeaveTypes}
        isAdmin={false}
      />,
    );

    const cell = screen.getByTestId('day-total-2026-05-18');
    expect(cell.textContent).toBe('8');
    expect(cell.className).not.toContain('text-red-600');
  });

  it('flush() short-circuits without calling the API when a day total exceeds the cap', async () => {
    // Pre-populate Monday with TaskA = 5h. User then edits TaskB Monday = 4h →
    // day total 9h, over cap. Per-cell input validator caps at 8, so the cap can
    // only be reached by combining valid cells across rows.
    const initial = makeInitialData({
      timeEntries: [{ contractTaskId: TASK_A, date: '2026-05-18', durationHours: 5, taskName: 'Task A' }],
    } as Partial<TimesheetWeek>);

    render(
      <TimesheetWeekGrid
        userId={USER_ID}
        year={2026}
        week={21}
        initialData={initial}
        selectableTasks={selectableTasks}
        selectableLeaveTypes={selectableLeaveTypes}
        isAdmin={false}
      />,
    );

    // Add a second task row (Task B) via the "Add task row" popover.
    fireEvent.click(screen.getByRole('button', { name: /Add task row/i }));
    fireEvent.click(screen.getByRole('button', { name: /Task B/i }));

    // Now edit TaskB's Monday cell to 4h. The TaskA row exists first; its 5 inputs
    // for business days come before TaskB's. TaskB's Monday is index 5 (after
    // TaskA's Mon..Fri = inputs[0..4]).
    const inputs = screen.getAllByRole('textbox');
    const taskBMondayCell = inputs[5];
    fireEvent.focus(taskBMondayCell);
    fireEvent.change(taskBMondayCell, { target: { value: '4' } });

    // Click Save.
    const saveButton = screen.getByRole('button', { name: /^Save$/i });
    fireEvent.click(saveButton);

    // Allow microtasks for the synchronous short-circuit branch of flush().
    await Promise.resolve();
    await Promise.resolve();

    expect(submitTimesheetBookingsMock).not.toHaveBeenCalled();

    // Banner shows the same message the backend would return.
    expect(screen.getByText(/One or more days exceed the daily capacity/i)).toBeTruthy();
  });
});
