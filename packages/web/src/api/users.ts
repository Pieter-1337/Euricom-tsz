import { type components } from './schema';
import { apiClient as client } from '#/lib/api.server';
import { ApiRequestError } from '#/api/client';

export type User = components['schemas']['UserDto'];
export type CreateUserRequest = components['schemas']['CreateUserCommand'];
export type UpdateUserRequest = components['schemas']['UpdateUserCommand'];

export const UserRole = {
  User: 'User',
  Admin: 'Admin',
  ClientManager: 'ClientManager',
} as const satisfies Record<string, components['schemas']['UserRole']>;
export type UserRole = (typeof UserRole)[keyof typeof UserRole];
export const USER_ROLES = [UserRole.User, UserRole.Admin, UserRole.ClientManager] as const;

export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const resp = await client.GET('/api/users/me');
    return resp.data ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const getUsers = async (): Promise<User[]> => {
  const resp = await client.GET('/api/users');
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
