import { apiClient as client } from '#/lib/api.server';
import type { LeaveType } from '#/api/leave-types';

export const getLeaveTypes = async (): Promise<LeaveType[]> => {
  const resp = await client.GET('/api/leave-types');
  return resp.data ?? [];
};
