import { apiClient as client } from '#/server/api-client.server';
import type { UserLeave, UpdateUserLeavesBody } from '#/api/user-leaves';

export const getUserLeaves = async (userId: string, year?: number): Promise<UserLeave[]> => {
  const resp = await client.GET('/api/users/{userId}/leaves', {
    params: { path: { userId }, query: year !== undefined ? { year } : {} },
  });
  return resp.data ?? [];
};

export const updateUserLeaves = async (userId: string, body: UpdateUserLeavesBody): Promise<UserLeave[]> => {
  const resp = await client.PUT('/api/users/{userId}/leaves', {
    params: { path: { userId } },
    body,
  });
  return resp.data ?? [];
};
