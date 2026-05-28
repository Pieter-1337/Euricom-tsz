import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import {
  getTimesheetWeek,
  applyTimesheetBookings,
  getSelectableContractTasks,
  getSelectableLeaveTypes,
  submitTimesheetWeek,
  approveTimesheetWeek,
  reopenTimesheetWeek,
  getTimesheetMonth,
  getPendingApprovals,
} from '#/api/timesheets.server';
import { throwApiError } from '#/lib/server-error';
import type { ApplyBookingsRequest } from '#/api/timesheets';

const weekParamsSchema = z.object({
  userId: z.string().uuid(),
  year: z.number().int(),
  week: z.number().int().min(1).max(53),
});

const timeEntryInputSchema = z.object({
  contractTaskId: z.string().uuid(),
  date: z.string(),
  durationHours: z.number(),
});

const leaveBookingInputSchema = z.object({
  leaveTypeId: z.string().uuid(),
  date: z.string(),
  durationHours: z.number(),
});

const applyBookingsInputSchema = weekParamsSchema.extend({
  timeEntries: z.array(timeEntryInputSchema),
  leaveBookings: z.array(leaveBookingInputSchema),
});

export const fetchTimesheetWeek = createServerFn({ method: 'GET' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => getTimesheetWeek(data.userId, data.year, data.week));

export const fetchSelectableContractTasks = createServerFn({ method: 'GET' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => getSelectableContractTasks(data.userId, data.year, data.week));

export const fetchSelectableLeaveTypes = createServerFn({ method: 'GET' })
  .inputValidator(z.object({ userId: z.string().uuid() }))
  .handler(async ({ data }) => getSelectableLeaveTypes(data.userId));

export const submitTimesheetBookings = createServerFn({ method: 'POST' })
  .inputValidator(applyBookingsInputSchema)
  .handler(async ({ data }) => {
    try {
      const body: ApplyBookingsRequest = {
        timeEntries: data.timeEntries,
        leaveBookings: data.leaveBookings,
      };
      return await applyTimesheetBookings(data.userId, data.year, data.week, body);
    } catch (e) {
      throwApiError(e);
    }
  });

export const submitWeekLifecycle = createServerFn({ method: 'POST' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => {
    try {
      await submitTimesheetWeek(data.userId, data.year, data.week);
    } catch (e) {
      throwApiError(e);
    }
  });

export const approveWeekLifecycle = createServerFn({ method: 'POST' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => {
    try {
      await approveTimesheetWeek(data.userId, data.year, data.week);
    } catch (e) {
      throwApiError(e);
    }
  });

export const reopenWeekLifecycle = createServerFn({ method: 'POST' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => {
    try {
      await reopenTimesheetWeek(data.userId, data.year, data.week);
    } catch (e) {
      throwApiError(e);
    }
  });

const monthParamsSchema = z.object({
  userId: z.string().uuid(),
  year: z.number().int(),
  month: z.number().int().min(1).max(12),
});

export const fetchTimesheetMonth = createServerFn({ method: 'GET' })
  .inputValidator(monthParamsSchema)
  .handler(async ({ data }) => getTimesheetMonth(data.userId, data.year, data.month));

export const fetchPendingApprovals = createServerFn({ method: 'GET' }).handler(async () =>
  getPendingApprovals(),
);
