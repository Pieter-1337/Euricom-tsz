import { z } from 'zod';

export type PendingApprovalSortKey = 'employee' | 'week' | 'totalHours';

export type PendingApprovalsExtraParams = {
  dateFrom?: string;
  dateTo?: string;
};

export const getPendingApprovalsPagedParamsSchema = z.object({
  search: z.string().optional(),
  sortBy: z.enum(['employee', 'week', 'totalHours']).optional(),
  sortDir: z.enum(['asc', 'desc']).optional(),
  pageSize: z.number().optional(),
  cursor: z.string().optional(),
  deletedOnly: z.boolean().optional(),
  dateFrom: z.string().optional(),
  dateTo: z.string().optional(),
});
