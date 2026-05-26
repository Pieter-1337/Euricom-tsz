import { useQuery } from '@tanstack/react-query';
import { computeLeaveSummary, type LeaveSummaryResult } from '#/features/leaves/compute-leave-summary';
import { fetchUserLeavesForYear, fetchLeaveBookingsForYear, fetchHolidaysForYear } from '#/features/leaves/server-fns';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const WORKDAY_CAPACITY = 8;

export interface UseLeaveSummaryResult {
  bookings: LeaveBookingForYear[];
  holidays: HolidayDto[];
  summary: LeaveSummaryResult;
  isPending: boolean;
}

export function useLeaveSummary(userId: string, year: number): UseLeaveSummaryResult {
  const leavesQuery = useQuery({
    queryKey: ['user-leaves', userId, year],
    queryFn: () => fetchUserLeavesForYear({ data: { userId, year } }),
  });

  const bookingsQuery = useQuery({
    queryKey: ['leave-bookings', userId, year],
    queryFn: () => fetchLeaveBookingsForYear({ data: { userId, year } }),
  });

  const holidaysQuery = useQuery({
    queryKey: ['holidays', year],
    queryFn: () => fetchHolidaysForYear({ data: { year } }),
  });

  const userLeaves = leavesQuery.data ?? [];
  const bookings = bookingsQuery.data ?? [];
  const holidays = holidaysQuery.data ?? [];

  const summary = computeLeaveSummary(userLeaves, bookings, holidays, WORKDAY_CAPACITY);

  const isPending = leavesQuery.isPending || bookingsQuery.isPending || holidaysQuery.isPending;

  return { bookings, holidays, summary, isPending };
}
