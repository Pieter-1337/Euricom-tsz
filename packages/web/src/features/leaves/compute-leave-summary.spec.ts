import { describe, it, expect } from 'vite-plus/test';
import { computeLeaveSummary } from './compute-leave-summary';
import type { UserLeave } from '#/api/user-leaves';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const CAPACITY = 8;

const makeLeave = (
  overrides: Partial<UserLeave> & Pick<UserLeave, 'id' | 'leaveTypeId' | 'leaveTypeName' | 'defaultAllowed' | 'year'>,
): UserLeave => ({
  totalDays: null,
  ...overrides,
});

const makeBooking = (leaveTypeId: string, durationHours: number): LeaveBookingForYear => ({
  date: '2026-01-10',
  leaveTypeId,
  leaveTypeName: 'x',
  durationHours,
});

const makeHoliday = (date: string): HolidayDto => ({
  date,
  name: 'Holiday',
  type: 'Public',
});

const LT_A = '00000000-0000-0000-0000-000000000001';
const LT_B = '00000000-0000-0000-0000-000000000002';

describe('computeLeaveSummary', () => {
  it('handles empty inputs gracefully', () => {
    const result = computeLeaveSummary([], [], [], CAPACITY);

    expect(result.rows).toHaveLength(1);
    expect(result.rows[0].leaveTypeId).toBe('__feestdagen__');
    expect(result.rows[0].taken).toBe(0);
    expect(result.totals).toEqual({ total: 0, taken: 0, balance: 0 });
  });

  it('Limited row: numeric Total / Taken / Balance', () => {
    const leave = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 20,
    });
    const booking = makeBooking(LT_A, 8);

    const result = computeLeaveSummary([leave], [booking], [], CAPACITY);

    const row = result.rows.find((r) => r.leaveTypeId === LT_A);
    expect(row?.total).toBe(20);
    expect(row?.taken).toBe(1);
    expect(row?.balance).toBe(19);
  });

  it('Unlimited row: numeric Taken; — for Total / Balance', () => {
    const leave = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'ADV',
      defaultAllowed: 'Unlimited',
      year: 2026,
    });
    const booking = makeBooking(LT_A, 16);

    const result = computeLeaveSummary([leave], [booking], [], CAPACITY);

    const row = result.rows.find((r) => r.leaveTypeId === LT_A);
    expect(row?.total).toBe('—');
    expect(row?.taken).toBe(2);
    expect(row?.balance).toBe('—');
  });

  it('Taken formula: sums multiple bookings of the same type', () => {
    const leave = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 25,
    });
    const bookings = [makeBooking(LT_A, 4), makeBooking(LT_A, 4), makeBooking(LT_A, 8)];

    const result = computeLeaveSummary([leave], bookings, [], CAPACITY);

    const row = result.rows.find((r) => r.leaveTypeId === LT_A);
    expect(row?.taken).toBe(2);
    expect(row?.balance).toBe(23);
  });

  it('Multiple LeaveTypes: each gets its own row, no cross-contamination', () => {
    const leaveA = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 20,
    });
    const leaveB = makeLeave({
      id: '2',
      leaveTypeId: LT_B,
      leaveTypeName: 'Ziekteverlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 10,
    });
    const bookings = [makeBooking(LT_A, 8), makeBooking(LT_B, 16)];

    const result = computeLeaveSummary([leaveA, leaveB], bookings, [], CAPACITY);

    const rowA = result.rows.find((r) => r.leaveTypeId === LT_A);
    const rowB = result.rows.find((r) => r.leaveTypeId === LT_B);

    expect(rowA?.taken).toBe(1);
    expect(rowA?.balance).toBe(19);
    expect(rowB?.taken).toBe(2);
    expect(rowB?.balance).toBe(8);
  });

  it('Feestdagen row: appears with the right count', () => {
    const holidays = [makeHoliday('2026-01-01'), makeHoliday('2026-04-05'), makeHoliday('2026-05-01')];

    const result = computeLeaveSummary([], [], holidays, CAPACITY);

    const row = result.rows.find((r) => r.leaveTypeId === '__feestdagen__');
    expect(row).toBeDefined();
    expect(row?.name).toBe('Holidays');
    expect(row?.total).toBe('—');
    expect(row?.taken).toBe(3);
    expect(row?.balance).toBe('—');
  });

  it('Totals row: sums Limited only, excludes Unlimited and Feestdagen', () => {
    const limited = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 20,
    });
    const unlimited = makeLeave({
      id: '2',
      leaveTypeId: LT_B,
      leaveTypeName: 'ADV',
      defaultAllowed: 'Unlimited',
      year: 2026,
    });
    const bookings = [makeBooking(LT_A, 8), makeBooking(LT_B, 16)];
    const holidays = [makeHoliday('2026-01-01')];

    const result = computeLeaveSummary([limited, unlimited], bookings, holidays, CAPACITY);

    expect(result.totals.total).toBe(20);
    expect(result.totals.taken).toBe(1);
    expect(result.totals.balance).toBe(19);
  });

  it('Row ordering: user leave rows first in order, then Feestdagen', () => {
    const leaveA = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: 20,
    });
    const leaveB = makeLeave({
      id: '2',
      leaveTypeId: LT_B,
      leaveTypeName: 'ADV',
      defaultAllowed: 'Unlimited',
      year: 2026,
    });

    const result = computeLeaveSummary([leaveA, leaveB], [], [], CAPACITY);

    expect(result.rows[0].leaveTypeId).toBe(LT_A);
    expect(result.rows[1].leaveTypeId).toBe(LT_B);
    expect(result.rows[2].leaveTypeId).toBe('__feestdagen__');
  });

  it('Limited row with null totalDays defaults to 0 total', () => {
    const leave = makeLeave({
      id: '1',
      leaveTypeId: LT_A,
      leaveTypeName: 'Verlof',
      defaultAllowed: 'Limited',
      year: 2026,
      totalDays: null,
    });

    const result = computeLeaveSummary([leave], [], [], CAPACITY);

    const row = result.rows.find((r) => r.leaveTypeId === LT_A);
    expect(row?.total).toBe(0);
    expect(row?.taken).toBe(0);
    expect(row?.balance).toBe(0);
  });
});
