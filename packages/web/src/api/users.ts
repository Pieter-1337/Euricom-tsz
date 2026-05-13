import { type components } from './schema';

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
