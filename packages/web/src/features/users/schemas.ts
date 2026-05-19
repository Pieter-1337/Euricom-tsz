import { z } from 'zod';
import { USER_ROLES } from '#/api/users';

export type UserSortKey = 'name' | 'email';

export const userIdSchema = z.string().min(1);

export const createUserSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().min(1, 'Email is required').email('Must be a valid email'),
  roles: z.array(z.enum(USER_ROLES)).min(1, 'At least one role is required'),
});

export const updateUserSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  roles: z.array(z.enum(USER_ROLES)).min(1, 'At least one role is required'),
});

export const saveUserInputSchema = z.object({
  id: userIdSchema,
  user: updateUserSchema,
});

export const leavesFormSchema = z.object({
  items: z.array(
    z.object({
      id: z.string(),
      leaveTypeId: z.string(),
      totalDays: z.coerce.number().min(0).nullable(),
    }),
  ),
});

export const getUsersPagedParamsSchema = z.object({
  search: z.string().optional(),
  sortBy: z.enum(['name', 'email']).optional(),
  sortDir: z.enum(['asc', 'desc']).optional(),
  pageSize: z.number().optional(),
  cursor: z.string().optional(),
  deletedOnly: z.boolean().optional(),
});
