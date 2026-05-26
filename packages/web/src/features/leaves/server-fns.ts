import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import { getUserLeaves } from '#/api/user-leaves.server';
import { getLeaveBookingsForYear, getHolidaysForYear } from '#/api/leaves.server';
import type { UserLeave } from '#/api/user-leaves';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const userYearSchema = z.object({
  userId: z.string().uuid(),
  year: z.number().int(),
});

const yearSchema = z.object({
  year: z.number().int(),
});

export const fetchUserLeavesForYear = createServerFn({ method: 'GET' })
  .inputValidator(userYearSchema)
  .handler(async ({ data }): Promise<UserLeave[]> => getUserLeaves(data.userId, data.year));

export const fetchLeaveBookingsForYear = createServerFn({ method: 'GET' })
  .inputValidator(userYearSchema)
  .handler(async ({ data }): Promise<LeaveBookingForYear[]> => getLeaveBookingsForYear(data.userId, data.year));

export const fetchHolidaysForYear = createServerFn({ method: 'GET' })
  .inputValidator(yearSchema)
  .handler(async ({ data }): Promise<HolidayDto[]> => getHolidaysForYear(data.year));
