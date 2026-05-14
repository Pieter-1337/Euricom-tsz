import { apiClient as client } from '#/lib/api.server';
import type { UserLeave, UpdateUserLeaveRequest } from '#/api/user-leaves';

export const getUserLeaves = async (userId: string): Promise<UserLeave[]> => {
  const resp = await client.GET('/api/users/{userId}/leaves', {
    params: { path: { userId } },
  });
  return resp.data ?? [];
};

export const updateUserLeave = async (userId: string, id: string, body: UpdateUserLeaveRequest): Promise<void> => {
  await client.PUT('/api/users/{userId}/leaves/{id}', {
    params: { path: { userId, id } },
    body,
  });
};
