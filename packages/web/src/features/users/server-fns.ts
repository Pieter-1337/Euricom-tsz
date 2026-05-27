import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import {
  createUser,
  getImpersonationTargets,
  getUserById,
  getUsers,
  getUsersPaged,
  removeUser,
  updateUser,
} from '#/api/users.server';
import { USER_ROLES, type User } from '#/api/users';
import { getUserLeaves, updateUserLeaves } from '#/api/user-leaves.server';
import type { UserLeave, UpdateUserLeavesBody } from '#/api/user-leaves';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';
import { throwApiError } from '#/lib/server-error';
import {
  createUserSchema,
  getUsersPagedParamsSchema,
  saveUserInputSchema,
  userIdSchema,
  type UserSortKey,
} from '#/features/users/schemas';

export const fetchUsersPaged = createServerFn({ method: 'GET' })
  .inputValidator((input: unknown) => getUsersPagedParamsSchema.parse(input))
  .handler(async ({ data }): Promise<KeysetPage<User>> => getUsersPaged(data as KeysetQueryParams<UserSortKey>));

export const fetchImpersonationTargets = createServerFn({ method: 'GET' })
  .inputValidator((input: unknown) => getUsersPagedParamsSchema.parse(input))
  .handler(
    async ({ data }): Promise<KeysetPage<User>> => getImpersonationTargets(data as KeysetQueryParams<UserSortKey>),
  );

export const fetchUsersByRole = createServerFn({ method: 'GET' })
  .inputValidator(z.enum(USER_ROLES))
  .handler(async ({ data: role }): Promise<User[]> => getUsers(role));

export const fetchAllUsers = createServerFn({ method: 'GET' }).handler(async (): Promise<User[]> => getUsers());

export const submitCreateUser = createServerFn({ method: 'POST' })
  .inputValidator(createUserSchema)
  .handler(async ({ data }) => {
    try {
      return await createUser(data);
    } catch (e) {
      throwApiError(e);
    }
  });

export const fetchUserAndLeaves = createServerFn({ method: 'GET' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }) => {
    const [user, leaves] = await Promise.all([getUserById(id), getUserLeaves(id)]);
    return { user, leaves };
  });

export const saveUser = createServerFn({ method: 'POST' })
  .inputValidator(saveUserInputSchema)
  .handler(async ({ data }) => {
    try {
      await updateUser(data.id, {
        id: data.id,
        firstName: data.user.firstName,
        lastName: data.user.lastName,
        roles: data.user.roles,
      });
    } catch (e) {
      throwApiError(e);
    }
  });

export const deleteUser = createServerFn({ method: 'POST' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }) => {
    try {
      await removeUser(id);
    } catch (e) {
      throwApiError(e);
    }
  });

export const submitUpdateUserLeaves = createServerFn({ method: 'POST' })
  .inputValidator((input: unknown) => input as { userId: string; body: UpdateUserLeavesBody })
  .handler(async ({ data }): Promise<UserLeave[]> => {
    try {
      return await updateUserLeaves(data.userId, data.body);
    } catch (e) {
      throwApiError(e);
    }
  });
