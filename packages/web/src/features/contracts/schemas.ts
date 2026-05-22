import { z } from 'zod';

export type ContractSortKey = 'number' | 'subject' | 'start';

export const contractIdSchema = z.string().min(1);

export const getContractsPagedParamsSchema = z.object({
  search: z.string().optional(),
  sortBy: z.enum(['number', 'subject', 'start']).optional(),
  sortDir: z.enum(['asc', 'desc']).optional(),
  pageSize: z.number().optional(),
  cursor: z.string().optional(),
  deletedOnly: z.boolean().optional(),
  activeOnDate: z.string().optional(),
  customerId: z.string().optional(),
});

export const contractTaskFormSchema = z.object({
  id: z.string().nullable(),
  name: z.string().min(1, 'Task name is required').max(256),
  rate: z.number().positive('Rate must be greater than 0'),
  originalArchivedId: z.string().optional(), // INTERNAL — stripped before submit
});

export type ContractTaskFormValue = z.infer<typeof contractTaskFormSchema>;

export const contractFormSchema = z
  .object({
    subject: z.string().min(1, 'Subject is required').max(256),
    customerId: z.string().min(1, 'Customer is required'),
    clientManagerId: z.string(),
    start: z.string().min(1, 'Start date is required'),
    end: z.string(),
    consultantIds: z.array(z.string()),
    tasks: z.array(contractTaskFormSchema),
  })
  .refine((v) => v.end === '' || v.end >= v.start, {
    message: 'End date must be on or after the start date.',
    path: ['end'],
  })
  .refine((v) => new Set(v.consultantIds).size === v.consultantIds.length, {
    message: 'Consultant list must not contain duplicates.',
    path: ['consultantIds'],
  })
  .refine(
    (v) => {
      const names = v.tasks.map((t) => t.name.trim().toLowerCase());
      return new Set(names).size === names.length;
    },
    {
      message: 'Task names must be unique (case-insensitive).',
      path: ['tasks'],
    },
  );

export type ContractFormValues = z.infer<typeof contractFormSchema>;

export const saveContractInputSchema = z.object({
  id: contractIdSchema,
  contract: contractFormSchema,
});
