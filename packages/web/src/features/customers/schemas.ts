import { z } from 'zod';

export type CustomerSortKey = 'number' | 'name' | 'city' | 'contactEmail';

export const customerIdSchema = z.string().min(1);

export const customerFormSchema = z.object({
  name: z.string().min(1, 'Name is required').max(256),
  contactName: z.string().max(256),
  contactEmail: z.string().min(1, 'Contact email is required').email('Must be a valid email').max(256),
  street: z.string().max(256),
  zip: z.string().max(32),
  city: z.string().max(128),
  country: z.string().max(128),
});

export type CustomerFormValues = z.infer<typeof customerFormSchema>;

export const saveCustomerInputSchema = z.object({
  id: customerIdSchema,
  customer: customerFormSchema,
});

export const getCustomersPagedParamsSchema = z.object({
  search: z.string().optional(),
  sortBy: z.enum(['number', 'name', 'city', 'contactEmail']).optional(),
  sortDir: z.enum(['asc', 'desc']).optional(),
  pageSize: z.number().optional(),
  cursor: z.string().optional(),
  deletedOnly: z.boolean().optional(),
});
