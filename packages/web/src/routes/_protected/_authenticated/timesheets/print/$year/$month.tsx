import { createFileRoute, Link } from '@tanstack/react-router';
import { z } from 'zod';
import { Button } from '#/components/ui/button';
import { fetchTimesheetMonth } from '#/features/timesheets/server-fns';
import { TimesheetPrintDoc } from '#/features/timesheets/components/timesheet-print-doc';
import type { CurrentUser } from '#/server/current-user';

const searchSchema = z.object({
  customer: z.string(),
  contract: z.string(),
});

export const Route = createFileRoute('/_protected/_authenticated/timesheets/print/$year/$month')({
  validateSearch: searchSchema,
  loader: async ({ params, context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    const year = Number(params.year);
    const month = Number(params.month);
    const monthData = await fetchTimesheetMonth({ data: { userId: currentUser.id, year, month } });
    return { currentUser, year, month, monthData };
  },
  component: TimesheetPrintPage,
});

function TimesheetPrintPage() {
  const { currentUser, year, month, monthData } = Route.useLoaderData();
  const { customer, contract } = Route.useSearch();

  const consultantName = `${currentUser.firstName} ${currentUser.lastName}`;

  // Only approved weeks appear on the printable customer document.
  const docMonthData = monthData
    ? { ...monthData, weeks: monthData.weeks.filter((w) => w.status === 'Approved') }
    : { userId: currentUser.id, year, month, weeks: [], monthTotalHours: 0 };

  return (
    <div className="mx-auto max-w-[820px]">
      <div className="print:hidden mb-4 flex items-center gap-3">
        <Button variant="outline" size="sm" asChild>
          <Link to="/timesheets">Back</Link>
        </Button>
        <Button size="sm" onClick={() => window.print()}>
          Download PDF
        </Button>
      </div>

      <div className="rounded-lg bg-white p-10 shadow-lg ring-1 ring-black/5 print:max-w-none print:rounded-none print:p-0 print:shadow-none print:ring-0">
        <TimesheetPrintDoc
          monthData={docMonthData}
          year={year}
          month={month}
          customer={customer}
          contract={contract}
          consultantName={consultantName}
        />
      </div>
    </div>
  );
}
