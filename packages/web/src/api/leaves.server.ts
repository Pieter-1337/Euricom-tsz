import { apiClient as client } from '#/server/api-client.server';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

export const getLeaveBookingsForYear = async (userId: string, year: number): Promise<LeaveBookingForYear[]> => {
  const resp = await client.GET('/api/timesheet-weeks/{userId}/leave-bookings', {
    params: { path: { userId }, query: { year } },
  });
  return resp.data ?? [];
};

export const getHolidaysForYear = async (year: number): Promise<HolidayDto[]> => {
  const resp = await client.GET('/api/workdays/holidays', {
    params: { query: { year } },
  });
  return resp.data ?? [];
};
