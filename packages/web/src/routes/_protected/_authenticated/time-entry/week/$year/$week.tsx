import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { TimesheetWeekGrid } from '#/features/timesheets/components/week-grid/timesheet-week-grid';
import {
  fetchSelectableContractTasks,
  fetchSelectableLeaveTypes,
  fetchTimesheetWeek,
} from '#/features/timesheets/server-fns';
import { fetchUserById } from '#/features/users/server-fns';
import type { CurrentUser } from '#/server/current-user';
import { UserRole } from '#/api/users';

const weekSearchSchema = z.object({
  userId: z.string().uuid().optional(),
});

export const Route = createFileRoute('/_protected/_authenticated/time-entry/week/$year/$week')({
  validateSearch: weekSearchSchema,
  loaderDeps: ({ search }) => ({ userId: search.userId }),
  loader: async ({ params, context, deps }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    const isAdmin = currentUser.roles.includes(UserRole.Admin);
    const year = Number(params.year);
    const week = Number(params.week);

    // Only honour the userId search param for admins; everyone else loads their own week.
    const targetUserId = isAdmin && deps.userId && deps.userId !== currentUser.id ? deps.userId : currentUser.id;

    const isViewingOther = targetUserId !== currentUser.id;

    const [weekData, selectableTasks, selectableLeaveTypes, targetUser] = await Promise.all([
      fetchTimesheetWeek({ data: { userId: targetUserId, year, week } }),
      fetchSelectableContractTasks({ data: { userId: targetUserId, year, week } }),
      fetchSelectableLeaveTypes({ data: { userId: targetUserId } }),
      isViewingOther ? fetchUserById({ data: targetUserId }) : Promise.resolve(null),
    ]);

    return {
      userId: targetUserId,
      year,
      week,
      weekData,
      selectableTasks,
      selectableLeaveTypes,
      isAdmin,
      isReadOnly: isViewingOther,
      targetUserName: isViewingOther && targetUser ? `${targetUser.firstName} ${targetUser.lastName}`.trim() : null,
    };
  },
  component: TimesheetWeekPage,
});

function TimesheetWeekPage() {
  const { userId, year, week, weekData, selectableTasks, selectableLeaveTypes, isAdmin, isReadOnly, targetUserName } =
    Route.useLoaderData();

  return (
    <main>
      {isReadOnly && targetUserName ? (
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[#3A4651] dark:text-white">{targetUserName}</h1>
          <p className="mt-0.5 text-sm text-[#6B7682] dark:text-white/40">
            Week {week}, {year} — read only
          </p>
        </div>
      ) : (
        <h1 className="mb-6 text-2xl font-bold">Time Entry</h1>
      )}
      <TimesheetWeekGrid
        key={`${userId}-${year}-${week}`}
        userId={userId}
        year={year}
        week={week}
        initialData={weekData}
        selectableTasks={selectableTasks}
        selectableLeaveTypes={selectableLeaveTypes}
        isAdmin={isAdmin}
        isReadOnly={isReadOnly}
      />
    </main>
  );
}
