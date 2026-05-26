import { LeaveAllowed, type UserLeave } from '#/api/user-leaves';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

export type DisplayValue = number | '—';

export interface LeaveSummaryRow {
  leaveTypeId: string | null;
  name: string;
  total: DisplayValue;
  taken: DisplayValue;
  balance: DisplayValue;
}

export interface LeaveTotals {
  total: number;
  taken: number;
  balance: number;
}

export interface LeaveSummaryResult {
  rows: LeaveSummaryRow[];
  totals: LeaveTotals;
}

const FEESTDAGEN_KEY = '__feestdagen__';

export function computeLeaveSummary(
  userLeaves: UserLeave[],
  bookings: LeaveBookingForYear[],
  holidays: HolidayDto[],
  workdayCapacity: number,
): LeaveSummaryResult {
  const bookingsByLeaveType = new Map<string, number>();
  for (const booking of bookings) {
    const prev = bookingsByLeaveType.get(booking.leaveTypeId) ?? 0;
    bookingsByLeaveType.set(booking.leaveTypeId, prev + booking.durationHours);
  }

  const rows: LeaveSummaryRow[] = [];
  let limitedTotal = 0;
  let limitedTaken = 0;
  let limitedBalance = 0;

  for (const leave of userLeaves) {
    const totalHours = bookingsByLeaveType.get(leave.leaveTypeId) ?? 0;
    const takenDays = totalHours / workdayCapacity;

    if (leave.defaultAllowed === LeaveAllowed.Limited) {
      const totalDays = leave.totalDays ?? 0;
      const balanceDays = totalDays - takenDays;

      limitedTotal += totalDays;
      limitedTaken += takenDays;
      limitedBalance += balanceDays;

      rows.push({
        leaveTypeId: leave.leaveTypeId,
        name: leave.leaveTypeName,
        total: totalDays,
        taken: takenDays,
        balance: balanceDays,
      });
    } else if (leave.defaultAllowed === LeaveAllowed.Unlimited) {
      rows.push({
        leaveTypeId: leave.leaveTypeId,
        name: leave.leaveTypeName,
        total: '—',
        taken: takenDays,
        balance: '—',
      });
    }
  }

  rows.push({
    leaveTypeId: FEESTDAGEN_KEY,
    name: 'Feestdagen',
    total: '—',
    taken: holidays.length,
    balance: '—',
  });

  return {
    rows,
    totals: {
      total: limitedTotal,
      taken: limitedTaken,
      balance: limitedBalance,
    },
  };
}
