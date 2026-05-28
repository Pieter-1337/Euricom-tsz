import { apiClient as client } from '#/server/api-client.server';
import type {
  TimesheetWeek,
  TimesheetMonth,
  SelectableContractTask,
  SelectableLeaveType,
  ApplyBookingsRequest,
  PendingApproval,
} from '#/api/timesheets';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';
import type { PendingApprovalSortKey, PendingApprovalsExtraParams } from '#/features/my-tasks/schemas';

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

export const getSelectableLeaveTypes = async (userId: string): Promise<SelectableLeaveType[]> => {
  const resp = await client.GET('/api/timesheet-selectable-leave-types/{userId}', {
    params: { path: { userId } },
  });
  return resp.data ?? [];
};

export const submitTimesheetWeek = async (userId: string, year: number, week: number): Promise<void> => {
  await client.POST('/api/timesheet-weeks/{userId}/{year}/{week}/submit', {
    params: { path: { userId, year, week } },
  });
};

export const approveTimesheetWeek = async (userId: string, year: number, week: number): Promise<void> => {
  await client.POST('/api/timesheet-weeks/{userId}/{year}/{week}/approve', {
    params: { path: { userId, year, week } },
  });
};

export const reopenTimesheetWeek = async (userId: string, year: number, week: number): Promise<void> => {
  await client.POST('/api/timesheet-weeks/{userId}/{year}/{week}/reopen', {
    params: { path: { userId, year, week } },
  });
};

export const getTimesheetMonth = async (
  userId: string,
  year: number,
  month: number,
): Promise<TimesheetMonth | null> => {
  const resp = await client.GET('/api/timesheets/{userId}/{year}/{month}', {
    params: { path: { userId, year, month } },
  });
  return resp.data ?? null;
};

export const getPendingApprovals = async (
  params: KeysetQueryParams<PendingApprovalSortKey> & PendingApprovalsExtraParams,
): Promise<KeysetPage<PendingApproval>> => {
  const resp = await client.GET('/api/timesheet-weeks/pending-approvals', {
    params: {
      query: {
        search: params.search,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        pageSize: params.pageSize,
        cursor: params.cursor,
        deletedOnly: params.deletedOnly,
        dateFrom: params.dateFrom,
        dateTo: params.dateTo,
      },
    },
  });
  return resp.data ?? { items: [], nextCursor: null, total: 0 };
};
