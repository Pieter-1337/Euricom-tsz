import { createFileRoute, redirect } from '@tanstack/react-router';
import { todayWeek } from '#/features/timesheets/iso-week';

export const Route = createFileRoute('/_protected/_authenticated/timesheets/')({
  beforeLoad: () => {
    const { year, week } = todayWeek();
    throw redirect({ to: '/timesheets/week/$year/$week', params: { year: String(year), week: String(week) } });
  },
});
