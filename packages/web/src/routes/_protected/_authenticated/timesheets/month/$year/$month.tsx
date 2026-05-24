import { createFileRoute } from '@tanstack/react-router';
import { TimesheetMonthGrid } from '#/features/timesheets/components/timesheet-month-grid';
import { fetchTimesheetMonth } from '#/features/timesheets/server-fns';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/timesheets/month/$year/$month')({
  loader: async ({ params, context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    const userId = currentUser.id;
    const year = Number(params.year);
    const month = Number(params.month);
    const monthData = await fetchTimesheetMonth({ data: { userId, year, month } });
    return { userId, year, month, monthData };
  },
  component: TimesheetMonthPage,
});

function TimesheetMonthPage() {
  const { userId, year, month, monthData } = Route.useLoaderData();

  const emptyMonth = {
    userId,
    year,
    month,
    weeks: [],
    monthTotalHours: 0,
  };

  return (
    <main>
      <h1 className="mb-6 text-2xl font-bold">My Timesheet</h1>
      <TimesheetMonthGrid month={monthData ?? emptyMonth} year={year} monthNum={month} userId={userId} />
    </main>
  );
}
