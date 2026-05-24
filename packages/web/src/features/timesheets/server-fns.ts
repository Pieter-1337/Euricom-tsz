import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import {
  getTimesheetWeek,
  applyTimesheetBookings,
  getSelectableContractTasks,
  submitTimesheetWeek,
  approveTimesheetWeek,
  reopenTimesheetWeek,
} from '#/api/timesheets.server';
import { throwApiError } from '#/lib/server-error';
import type { ApplyBookingsRequest } from '#/api/timesheets';

const weekParamsSchema = z.object({
  userId: z.string().uuid(),
  year: z.number().int(),
  week: z.number().int().min(1).max(53),
});

const applyBookingsInputSchema = weekParamsSchema.extend({
  bookings: z.array(
    z.object({
      contractTaskId: z.string().uuid(),
      date: z.string(),
      durationHours: z.number(),
    }),
  ),
});

export const fetchTimesheetWeek = createServerFn({ method: 'GET' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => getTimesheetWeek(data.userId, data.year, data.week));

export const fetchSelectableContractTasks = createServerFn({ method: 'GET' })
  .inputValidator(weekParamsSchema)
  .handler(async ({ data }) => getSelectableContractTasks(data.userId, data.year, data.week));

export const submitTimesheetBookings = createServerFn({ method: 'POST' })
  .inputValidator(applyBookingsInputSchema)
  .handler(async ({ data }) => {
    try {
      const body: ApplyBookingsRequest = { bookings: data.bookings };
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
