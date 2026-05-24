import { createFileRoute } from '@tanstack/react-router';
import { TimesheetWeekGrid } from '#/features/timesheets/components/timesheet-week-grid';
import {
  fetchSelectableContractTasks,
  fetchSelectableLeaveTypes,
  fetchTimesheetWeek,
} from '#/features/timesheets/server-fns';
import type { CurrentUser } from '#/server/current-user';
import { UserRole } from '#/api/users';

export const Route = createFileRoute('/_protected/_authenticated/timesheets/week/$year/$week')({
  loader: async ({ params, context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    const userId = currentUser.id;
    const isAdmin = currentUser.roles.includes(UserRole.Admin);
    const year = Number(params.year);
    const week = Number(params.week);
    const [weekData, selectableTasks, selectableLeaveTypes] = await Promise.all([
      fetchTimesheetWeek({ data: { userId, year, week } }),
      fetchSelectableContractTasks({ data: { userId, year, week } }),
      fetchSelectableLeaveTypes({ data: { userId } }),
    ]);
    return { userId, year, week, weekData, selectableTasks, selectableLeaveTypes, isAdmin };
  },
  component: TimesheetWeekPage,
});

function TimesheetWeekPage() {
  const { userId, year, week, weekData, selectableTasks, selectableLeaveTypes, isAdmin } = Route.useLoaderData();

  return (
    <main>
      <h1 className="mb-6 text-2xl font-bold">My Timesheet</h1>
      <TimesheetWeekGrid
        key={`${year}-${week}`}
        userId={userId}
        year={year}
        week={week}
        initialData={weekData}
        selectableTasks={selectableTasks}
        selectableLeaveTypes={selectableLeaveTypes}
        isAdmin={isAdmin}
      />
    </main>
  );
}
