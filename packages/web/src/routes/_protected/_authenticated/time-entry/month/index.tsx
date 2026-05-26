import { createFileRoute, redirect } from '@tanstack/react-router';
import { todayMonth } from '#/features/timesheets/iso-week';

export const Route = createFileRoute('/_protected/_authenticated/time-entry/month/')({
  beforeLoad: () => {
    const { year, month } = todayMonth();
    throw redirect({ to: '/time-entry/month/$year/$month', params: { year: String(year), month: String(month) } });
  },
});
