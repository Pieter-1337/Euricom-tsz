import { apiClient as client } from '#/server/api-client.server';
import type { TimesheetWeek, SelectableContractTask, ApplyBookingsRequest } from '#/api/timesheets';

export const getTimesheetWeek = async (userId: string, year: number, week: number): Promise<TimesheetWeek | null> => {
  const resp = await client.GET('/api/timesheet-weeks/{userId}/{year}/{week}', {
    params: { path: { userId, year, week } },
  });
  return resp.data ?? null;
};

export const applyTimesheetBookings = async (
  userId: string,
  year: number,
  week: number,
  body: ApplyBookingsRequest,
): Promise<TimesheetWeek> => {
  const resp = await client.PUT('/api/timesheet-weeks/{userId}/{year}/{week}/bookings', {
    params: { path: { userId, year, week } },
    body,
  });
  return resp.data!;
};

export const getSelectableContractTasks = async (
  userId: string,
  year: number,
  week: number,
): Promise<SelectableContractTask[]> => {
  const resp = await client.GET('/api/timesheet-selectable-tasks/{userId}/{year}/{week}', {
    params: { path: { userId, year, week } },
  });
  return resp.data ?? [];
};
