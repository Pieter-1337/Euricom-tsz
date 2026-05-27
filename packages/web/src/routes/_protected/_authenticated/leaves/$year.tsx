import { createFileRoute, Link, redirect } from '@tanstack/react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { LeaveYearGrid } from '#/features/leaves/components/leave-year-grid';
import { LeaveBalancePanel } from '#/features/leaves/components/leave-balance-panel';
import { useLeaveSummary } from '#/features/leaves/use-leave-summary';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/leaves/$year')({
  beforeLoad: ({ params }) => {
    const parsed = Number(params.year);
    // Guard against non-numeric or absurd years (e.g. /leaves/foo) that would
    // otherwise produce NaN and break the year-scoped queries.
    if (!Number.isInteger(parsed) || parsed < 1900 || parsed > 2200) {
      throw redirect({
        to: '/leaves/$year',
        params: { year: String(new Date().getFullYear()) },
      });
    }
  },
  component: LeaveOverviewPage,
});

function LeaveOverviewPage() {
  const { currentUser } = Route.useRouteContext() as { currentUser: CurrentUser };
  const { year: yearParam } = Route.useParams();
  const year = Number(yearParam);

  const { bookings, holidays, summary, isPending } = useLeaveSummary(currentUser.id, year);

  const hasNoLeaves = !isPending && summary.rows.filter((r) => r.leaveTypeId !== '__feestdagen__').length === 0;

  return (
    <main className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Leave overview</h1>
        <div className="flex items-center gap-1.5">
          <Button variant="outline" size="sm" asChild aria-label="Previous year">
            <Link to="/leaves/$year" params={{ year: String(year - 1) }}>
              <ChevronLeft className="size-4" strokeWidth={1.75} />
            </Link>
          </Button>
          <span className="min-w-[3.5rem] text-center text-sm font-semibold tabular-nums">{year}</span>
          <Button variant="outline" size="sm" asChild aria-label="Next year">
            <Link to="/leaves/$year" params={{ year: String(year + 1) }}>
              <ChevronRight className="size-4" strokeWidth={1.75} />
            </Link>
          </Button>
        </div>
      </div>

      {hasNoLeaves && (
        <div className="rounded-[12px] border border-black/[0.08] bg-white px-5 py-6 text-center dark:border-white/[0.06] dark:bg-[#1D252D]">
          <p className="text-sm text-[#6B7682] dark:text-white/50">No leave configured for {year}.</p>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_340px] lg:items-start">
        <LeaveYearGrid year={year} bookings={bookings} holidays={holidays} />
        <LeaveBalancePanel summary={summary} />
      </div>
    </main>
  );
}
