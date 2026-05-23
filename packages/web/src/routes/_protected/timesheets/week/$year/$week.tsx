import { createFileRoute } from '@tanstack/react-router';
import { TimesheetWeekGrid } from '#/features/timesheets/components/timesheet-week-grid';
import { fetchSelectableContractTasks, fetchTimesheetWeek } from '#/features/timesheets/server-fns';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/timesheets/week/$year/$week')({
  loader: async ({ params, context }) => {
    const currentUser = (context as { currentUser?: CurrentUser }).currentUser;
    if (!currentUser) throw new Error('Not authenticated');
    const userId = currentUser.id;
    const year = Number(params.year);
    const week = Number(params.week);
    const [weekData, selectableTasks] = await Promise.all([
      fetchTimesheetWeek({ data: { userId, year, week } }),
      fetchSelectableContractTasks({ data: { userId, year, week } }),
    ]);
    return { userId, year, week, weekData, selectableTasks };
  },
  component: TimesheetWeekPage,
});

function TimesheetWeekPage() {
  const { userId, year, week, weekData, selectableTasks } = Route.useLoaderData();

  return (
    <main>
      <h1 className="mb-6 text-2xl font-bold">My Timesheet</h1>
      <TimesheetWeekGrid
        userId={userId}
        year={year}
        week={week}
        initialData={weekData}
        selectableTasks={selectableTasks}
      />
    </main>
  );
}
