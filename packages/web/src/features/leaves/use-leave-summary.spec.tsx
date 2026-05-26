// @vitest-environment jsdom
import { describe, it, expect, vi } from 'vite-plus/test';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { UserLeave } from '#/api/user-leaves';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const { fetchUserLeavesForYearMock, fetchLeaveBookingsForYearMock, fetchHolidaysForYearMock } = vi.hoisted(() => ({
  fetchUserLeavesForYearMock: vi.fn(),
  fetchLeaveBookingsForYearMock: vi.fn(),
  fetchHolidaysForYearMock: vi.fn(),
}));

vi.mock('#/features/leaves/server-fns', () => ({
  fetchUserLeavesForYear: fetchUserLeavesForYearMock,
  fetchLeaveBookingsForYear: fetchLeaveBookingsForYearMock,
  fetchHolidaysForYear: fetchHolidaysForYearMock,
}));

import { useLeaveSummary } from './use-leave-summary';

const USER_ID = '00000000-0000-0000-0000-000000000001';
const LT_A = '00000000-0000-0000-0000-000000000010';
const YEAR = 2026;

function wrapper({ children }: { children: React.ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useLeaveSummary', () => {
  it('returns computed summary for canned data', async () => {
    const leaves: UserLeave[] = [
      {
        id: 'ul-1',
        leaveTypeId: LT_A,
        leaveTypeName: 'Verlof',
        defaultAllowed: 'Limited',
        year: YEAR,
        totalDays: 20,
        takenDays: null,
        balanceDays: null,
      },
    ];
    const bookings: LeaveBookingForYear[] = [
      { date: '2026-05-01', leaveTypeId: LT_A, leaveTypeName: 'Verlof', durationHours: 8 },
    ];
    const holidays: HolidayDto[] = [
      { date: '2026-01-01', name: 'Nieuwjaar', type: 'Public' },
      { date: '2026-04-05', name: 'Paaszondag', type: 'Public' },
    ];

    fetchUserLeavesForYearMock.mockResolvedValue(leaves);
    fetchLeaveBookingsForYearMock.mockResolvedValue(bookings);
    fetchHolidaysForYearMock.mockResolvedValue(holidays);

    const { result } = renderHook(() => useLeaveSummary(USER_ID, YEAR), { wrapper });

    await waitFor(() => expect(result.current.isPending).toBe(false));

    const { summary, bookings: rawBookings, holidays: rawHolidays } = result.current;

    expect(rawBookings).toEqual(bookings);
    expect(rawHolidays).toEqual(holidays);

    const verlofRow = summary.rows.find((r) => r.leaveTypeId === LT_A);
    expect(verlofRow?.total).toBe(20);
    expect(verlofRow?.taken).toBe(1);
    expect(verlofRow?.balance).toBe(19);

    const feestdagenRow = summary.rows.find((r) => r.leaveTypeId === '__feestdagen__');
    expect(feestdagenRow?.taken).toBe(2);

    expect(summary.totals.total).toBe(20);
    expect(summary.totals.taken).toBe(1);
    expect(summary.totals.balance).toBe(19);
  });

  it('returns empty summary when all queries return empty arrays', async () => {
    fetchUserLeavesForYearMock.mockResolvedValue([]);
    fetchLeaveBookingsForYearMock.mockResolvedValue([]);
    fetchHolidaysForYearMock.mockResolvedValue([]);

    const { result } = renderHook(() => useLeaveSummary(USER_ID, YEAR), { wrapper });

    await waitFor(() => expect(result.current.isPending).toBe(false));

    expect(result.current.summary.rows).toHaveLength(1);
    expect(result.current.summary.rows[0].leaveTypeId).toBe('__feestdagen__');
    expect(result.current.summary.totals).toEqual({ total: 0, taken: 0, balance: 0 });
  });
});
