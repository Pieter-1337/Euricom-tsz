import { apiClient as client } from '#/lib/api.server';
import { ApiRequestError } from '#/api/client';

export type UserRole = 'User' | 'Admin' | 'ClientManager';

export type User = {
  id: string;
  email: string;
  name: string;
  role: UserRole;
  holidayDays: number;
  advDays: number;
  ancienniteitDays: number;
  sicknessDays: number;
};

export type CreateUserRequest = {
  name: string;
  email: string;
  role: UserRole;
};

export type UpdateUserRequest = {
  id: string;
  name: string;
  role: UserRole;
};

// NOTE: hand-typed pending `bun --filter web gen:api` once the API dev server is up.
// Replace these types with `components['schemas']['User'|...]` from schema.ts after regen.

export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const resp = await (client as any).GET('/api/users/me');
    return (resp.data as User) ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const getUsers = async (): Promise<User[]> => {
  const resp = await (client as any).GET('/api/users');
  return (resp.data as User[]) ?? [];
};

export const getUserById = async (id: string): Promise<User | null> => {
  try {
    const resp = await (client as any).GET('/api/users/{id}', { params: { path: { id } } });
    return (resp.data as User) ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const createUser = async (body: CreateUserRequest): Promise<User> => {
  const resp = await (client as any).POST('/api/users', { body });
  return resp.data as User;
};

export const updateUser = async (id: string, body: UpdateUserRequest): Promise<User> => {
  const resp = await (client as any).PUT('/api/users/{id}', { params: { path: { id } }, body });
  return resp.data as User;
};

export const removeUser = async (id: string): Promise<void> => {
  await (client as any).DELETE('/api/users/{id}', { params: { path: { id } } });
};
