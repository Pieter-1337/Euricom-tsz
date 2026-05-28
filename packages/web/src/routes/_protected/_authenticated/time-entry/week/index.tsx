import { createFileRoute, redirect } from '@tanstack/react-router';
import { todayWeek } from '#/features/timesheets/iso-week';
import { z } from 'zod';

const weekIndexSearchSchema = z.object({
  userId: z.string().uuid().optional(),
});

export const Route = createFileRoute('/_protected/_authenticated/time-entry/week/')({
  validateSearch: weekIndexSearchSchema,
  beforeLoad: ({ search }) => {
    const { year, week } = todayWeek();
    throw redirect({
      to: '/time-entry/week/$year/$week',
      params: { year: String(year), week: String(week) },
      search: search.userId ? { userId: search.userId } : {},
    });
  },
});
