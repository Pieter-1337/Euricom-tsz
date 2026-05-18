import { createServerFn } from '@tanstack/react-start';
import { getCurrentUser as fetchCurrentUser } from '#/api/users.server';
import type { User } from '#/api/users';

export type CurrentUser = User;

export const getCurrentUser = createServerFn({ method: 'GET' }).handler(async (): Promise<CurrentUser | null> => {
  return await fetchCurrentUser();
});
