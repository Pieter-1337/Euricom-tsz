import { apiClient as client } from '#/server/api-client.server';
import { ApiRequestError } from '#/api/client';
import type { User, CreateUserRequest, UpdateUserRequest, UserRole } from '#/api/users';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';

type UserSortKey = 'name' | 'email';

export const getUsersPaged = async (params: KeysetQueryParams<UserSortKey>): Promise<KeysetPage<User>> => {
  const resp = await client.GET('/api/users/paged', {
    params: {
      query: {
        search: params.search,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        pageSize: params.pageSize,
        cursor: params.cursor,
        deletedOnly: params.deletedOnly,
      },
    },
  });
  return resp.data ?? { items: [], nextCursor: null, total: 0 };
};

export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const resp = await client.GET('/api/users/me');
    return resp.data ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    // eslint-disable-next-line no-console
    console.error('[getCurrentUser] fetch error', e, (e as { cause?: unknown })?.cause);
    throw e;
  }
};

export const getUsers = async (role?: UserRole): Promise<User[]> => {
  const resp = await client.GET('/api/users', { params: { query: { role } } });
  return resp.data ?? [];
};

export const getUserById = async (id: string): Promise<User | null> => {
  try {
    const resp = await client.GET('/api/users/{id}', { params: { path: { id } } });
    return resp.data ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const createUser = async (body: CreateUserRequest): Promise<User> => {
  const resp = await client.POST('/api/users', { body });
  return resp.data!;
};

export const updateUser = async (id: string, body: UpdateUserRequest): Promise<User> => {
  const resp = await client.PUT('/api/users/{id}', { params: { path: { id } }, body });
  return resp.data!;
};

export const removeUser = async (id: string): Promise<void> => {
  await client.DELETE('/api/users/{id}', { params: { path: { id } } });
};

export const getImpersonationTargets = async (params: KeysetQueryParams<UserSortKey>): Promise<KeysetPage<User>> => {
  const resp = await client.GET('/api/users/impersonation-targets', {
    params: {
      query: {
        search: params.search,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        pageSize: params.pageSize,
        cursor: params.cursor,
      },
    },
  });
  return resp.data ?? { items: [], nextCursor: null, total: 0 };
};
