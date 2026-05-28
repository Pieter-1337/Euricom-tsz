import { createFileRoute, Link, redirect } from '@tanstack/react-router';
import { Inbox } from 'lucide-react';
import { UserRole } from '#/api/users';
import { Button } from '#/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { fetchPendingApprovals } from '#/features/timesheets/server-fns';
import { isoWeekToMonday, formatIsoDate } from '#/features/timesheets/iso-week';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/my-tasks')({
  beforeLoad: ({ context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    if (!currentUser.roles.includes(UserRole.Admin)) throw redirect({ to: '/' });
  },
  loader: async () => {
    const approvals = await fetchPendingApprovals();
    // Oldest first: sort by year then week ascending
    return [...approvals].sort((a, b) => a.isoYear - b.isoYear || a.isoWeek - b.isoWeek);
  },
  component: MyTasksPage,
});

function weekDateRange(year: number, week: number): string {
  const monday = isoWeekToMonday(year, week);
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return `${formatIsoDate(monday)} – ${formatIsoDate(sunday)}`;
}

function MyTasksPage() {
  const approvals = Route.useLoaderData();

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold">My Tasks</h1>

      {approvals.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16 text-center">
          <Inbox className="h-10 w-10 text-[#6B7682] dark:text-white/30" strokeWidth={1.75} />
          <p className="text-sm font-medium text-[#6B7682] dark:text-white/40">No pending approvals</p>
          <p className="max-w-xs text-xs text-[#6B7682]/70 dark:text-white/25">
            All submitted timesheets have been reviewed. Check back later.
          </p>
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Employee</TableHead>
              <TableHead>ISO week</TableHead>
              <TableHead>Date range</TableHead>
              <TableHead className="text-right">Total hours</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {approvals.map((approval) => (
              <TableRow key={`${approval.userId}-${approval.isoYear}-${approval.isoWeek}`}>
                <TableCell className="font-medium">{approval.userName}</TableCell>
                <TableCell>
                  {approval.isoYear}-W{String(approval.isoWeek).padStart(2, '0')}
                </TableCell>
                <TableCell className="text-[#6B7682] dark:text-white/50">
                  {weekDateRange(approval.isoYear, approval.isoWeek)}
                </TableCell>
                <TableCell className="text-right">{approval.totalHours}h</TableCell>
                <TableCell className="text-right">
                  <Button asChild variant="outline" size="sm">
                    <Link
                      to="/time-entry/week/$year/$week"
                      params={{
                        year: String(approval.isoYear),
                        week: String(approval.isoWeek),
                      }}
                      search={{ userId: approval.userId }}
                    >
                      Open
                    </Link>
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
