import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, FileDown } from 'lucide-react';
import { useState } from 'react';
import { Button } from '#/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { Tooltip, TooltipProvider } from '#/components/ui/tooltip';
import { cn } from '#/lib/utils';
import { getLeaveColor } from '#/features/leaves/use-leave-colors';
import { fetchTimesheetMonth } from '#/features/timesheets/server-fns';
import {
  formatIsoDate,
  formatMonthLabel,
  nextMonth,
  prevMonth,
  todayMonth,
} from '#/features/timesheets/iso-week';
import type { TimesheetMonth, TimesheetMonthDay, TimesheetMonthWeek } from '#/api/timesheets';
import type { CurrentUser } from '#/server/current-user';

const DAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

export const Route = createFileRoute('/_protected/_authenticated/timesheets/')({
  loader: async ({ context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    const today = todayMonth();
    const monthData = await fetchTimesheetMonth({
      data: { userId: currentUser.id, year: today.year, month: today.month },
    });
    return { userId: currentUser.id, today, initialMonthData: monthData };
  },
  component: TimesheetsOverviewPage,
});

function TimesheetsOverviewPage() {
  const { userId, today, initialMonthData } = Route.useLoaderData();
  const [{ year, month }, setYM] = useState(today);

  const isInitial = year === today.year && month === today.month;
  const { data: monthData } = useQuery({
    queryKey: ['timesheet-month-overview', userId, year, month],
    queryFn: () => fetchTimesheetMonth({ data: { userId, year, month } }),
    initialData: isInitial ? initialMonthData ?? undefined : undefined,
  });

  return (
    <TooltipProvider delayDuration={100} skipDelayDuration={200}>
      <main className="space-y-4">
        <h1 className="text-2xl font-bold">Timesheets</h1>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[2fr_1fr]">
          <CalendarCard
            year={year}
            month={month}
            monthData={monthData ?? null}
            onPrev={() => setYM(prevMonth(year, month))}
            onNext={() => setYM(nextMonth(year, month))}
            onToday={() => setYM(todayMonth())}
          />
          <TotalsCard monthData={monthData ?? null} />
        </div>

        <TimesheetsListCard monthData={monthData ?? null} />
      </main>
    </TooltipProvider>
  );
}

interface CalendarCellData {
  isoDate: string;
  dayNumber: number;
  outside: boolean;
  day: TimesheetMonthDay | null;
  weekStatus: string | null;
}

function buildCalendarCells(
  year: number,
  month: number,
  monthData: TimesheetMonth | null,
): CalendarCellData[] {
  const firstOfMonth = new Date(year, month - 1, 1);
  const lastOfMonth = new Date(year, month, 0);

  const fromOffset = (firstOfMonth.getDay() === 0 ? 7 : firstOfMonth.getDay()) - 1;
  const calendarStart = new Date(firstOfMonth);
  calendarStart.setDate(firstOfMonth.getDate() - fromOffset);

  const dayMs = 24 * 3600 * 1000;
  const daysToCover = Math.round((lastOfMonth.getTime() - calendarStart.getTime()) / dayMs) + 1;
  const totalCells = Math.ceil(daysToCover / 7) * 7;

  const dayLookup = new Map<string, { day: TimesheetMonthDay; weekStatus: string }>();
  if (monthData) {
    for (const week of monthData.weeks) {
      for (const day of week.days) {
        dayLookup.set(day.date, { day, weekStatus: week.status });
      }
    }
  }

  const cells: CalendarCellData[] = [];
  for (let i = 0; i < totalCells; i++) {
    const d = new Date(calendarStart);
    d.setDate(calendarStart.getDate() + i);
    const iso = formatIsoDate(d);
    const lookup = dayLookup.get(iso);
    cells.push({
      isoDate: iso,
      dayNumber: d.getDate(),
      outside: d.getMonth() !== month - 1,
      day: lookup?.day ?? null,
      weekStatus: lookup?.weekStatus ?? null,
    });
  }
  return cells;
}

function CalendarCard({
  year,
  month,
  monthData,
  onPrev,
  onNext,
  onToday,
}: {
  year: number;
  month: number;
  monthData: TimesheetMonth | null;
  onPrev: () => void;
  onNext: () => void;
  onToday: () => void;
}) {
  const cells = buildCalendarCells(year, month, monthData);

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <header className="flex items-center justify-between px-5 py-4">
        <h2 className="text-base font-semibold capitalize">{formatMonthLabel(year, month)}</h2>
        <div className="flex items-center gap-1.5">
          <Button variant="outline" size="sm" onClick={onPrev} aria-label="Previous month">
            <ChevronLeft className="size-4" strokeWidth={1.75} />
          </Button>
          <Button variant="outline" size="sm" onClick={onToday}>
            Today
          </Button>
          <Button variant="outline" size="sm" onClick={onNext} aria-label="Next month">
            <ChevronRight className="size-4" strokeWidth={1.75} />
          </Button>
        </div>
      </header>

      <div className="grid grid-cols-7 border-t border-b border-black/[0.06] dark:border-white/[0.06]">
        {DAY_LABELS.map((d) => (
          <div
            key={d}
            className="px-2 py-2 text-center text-[10.5px] font-medium uppercase tracking-[0.32em] text-[#6B7682] dark:text-white/40"
          >
            {d}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7">
        {cells.map((cell) => (
          <DayCell key={cell.isoDate} cell={cell} />
        ))}
      </div>

      <footer className="px-5 py-3 text-xs italic text-[#6B7682] dark:text-white/40">
        * Not approved yet
      </footer>
    </section>
  );
}

function DayCell({ cell }: { cell: CalendarCellData }) {
  const approved = cell.weekStatus === 'Approved';
  const showAsterisk = cell.weekStatus !== null && cell.weekStatus !== 'Approved';

  return (
    <div
      className={cn(
        'flex min-h-[78px] flex-col gap-1 border-r border-b border-black/[0.06] px-1.5 py-1.5 dark:border-white/[0.06]',
        '[&:nth-child(7n)]:border-r-0',
        cell.outside && 'opacity-30',
      )}
    >
      <div className="text-right text-[11px] font-medium text-[#6B7682] dark:text-white/50">
        {cell.dayNumber}
      </div>
      {!cell.outside && cell.day && (
        <div className="flex flex-col gap-0.5">
          {cell.day.timeEntries.map((entry, i) => (
            <Tooltip
              key={`t-${i}`}
              content={`${entry.customerName} — ${entry.taskName} (${entry.durationHours}h)`}
            >
              <div
                className={cn(
                  'cursor-pointer truncate rounded-sm px-1.5 py-0.5 text-[10.5px] text-white',
                  approved ? 'bg-green-700 dark:bg-green-700' : 'bg-green-600 dark:bg-green-600',
                )}
              >
                {entry.durationHours} - {entry.customerName || entry.taskName}
                {showAsterisk && '*'}
              </div>
            </Tooltip>
          ))}
          {cell.day.leaveBookings.map((leave, i) => (
            <Tooltip key={`l-${i}`} content={`${leave.leaveTypeName} (${leave.durationHours}h)`}>
              <div
                className={cn(
                  'cursor-pointer truncate rounded-sm px-1.5 py-0.5 text-[10.5px] text-white',
                  getLeaveColor(leave.leaveTypeId).bg,
                )}
              >
                {leave.durationHours} - {leave.leaveTypeName}
                {showAsterisk && '*'}
              </div>
            </Tooltip>
          ))}
          {cell.day.holidayName && (
            <Tooltip content={cell.day.holidayName}>
              <div className="cursor-pointer truncate rounded-sm bg-amber-600/90 px-1.5 py-0.5 text-[10.5px] text-white">
                {cell.day.holidayName}
              </div>
            </Tooltip>
          )}
        </div>
      )}
    </div>
  );
}

interface BreakdownRow {
  key: string;
  label: string;
  hours: number;
  dot: string;
}

function buildBreakdown(
  weeks: TimesheetMonthWeek[],
  monthPrefix: string,
): { rows: BreakdownRow[]; dayCount: number } {
  const workedHours = new Map<string, number>();
  const leaveHours = new Map<string, { name: string; hours: number }>();
  const bookedDays = new Set<string>();

  for (const week of weeks) {
    for (const day of week.days) {
      const hasEntries = day.timeEntries.length > 0 || day.leaveBookings.length > 0;
      if (day.isBusinessDay && day.date.startsWith(monthPrefix) && hasEntries) {
        bookedDays.add(day.date);
      }
      for (const entry of day.timeEntries) {
        const cust = entry.customerName || 'Unknown';
        workedHours.set(cust, (workedHours.get(cust) ?? 0) + entry.durationHours);
      }
      for (const leave of day.leaveBookings) {
        const prev = leaveHours.get(leave.leaveTypeId);
        leaveHours.set(leave.leaveTypeId, {
          name: leave.leaveTypeName,
          hours: (prev?.hours ?? 0) + leave.durationHours,
        });
      }
    }
  }

  const rows = [
    ...Array.from(workedHours, ([name, hours]) => ({ key: `c-${name}`, label: name, hours, dot: 'bg-green-600' })),
    ...Array.from(leaveHours, ([id, v]) => ({ key: `l-${id}`, label: v.name, hours: v.hours, dot: getLeaveColor(id).bg })),
  ].sort((a, b) => b.hours - a.hours);

  return { rows, dayCount: bookedDays.size };
}

function BreakdownSection({
  title,
  rows,
  days,
  businessDays,
}: {
  title: string;
  rows: BreakdownRow[];
  days: number;
  businessDays: number;
}) {
  return (
    <div>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[#6B7682] dark:text-white/40">
        {title}
      </p>
      <div className="mt-2 border-b border-black/[0.06] pb-3 text-center text-sm font-semibold dark:border-white/[0.06]">
        {days} / {businessDays} workdays
      </div>
      <ul className="pt-3 text-sm">
        {rows.map((row) => (
          <li key={row.key} className="flex items-center justify-between gap-2 py-1">
            <span className="flex items-center gap-2 font-medium">
              <span className={cn('inline-block size-2.5 shrink-0 rounded-sm', row.dot)} aria-hidden="true" />
              {row.label}
            </span>
            <span className="text-[#6B7682] dark:text-white/60">
              {Math.round(row.hours * 100) / 100}h
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function TotalsCard({ monthData }: { monthData: TimesheetMonth | null }) {
  if (!monthData) {
    return (
      <section className="rounded-[12px] border border-black/[0.08] bg-white p-5 dark:border-white/[0.06] dark:bg-[#1D252D]">
        <h2 className="text-base font-semibold">Totals</h2>
      </section>
    );
  }

  const monthPrefix = `${monthData.year}-${String(monthData.month).padStart(2, '0')}`;
  const businessDaysInMonth = monthData.weeks
    .flatMap((w) => w.days)
    .filter((d) => d.isBusinessDay && d.date.startsWith(monthPrefix))
    .length;

  const approved = buildBreakdown(monthData.weeks.filter((w) => w.status === 'Approved'), monthPrefix);
  const notApproved = buildBreakdown(monthData.weeks.filter((w) => w.status !== 'Approved'), monthPrefix);

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="border-b border-black/[0.06] px-5 py-4 text-base font-semibold dark:border-white/[0.06]">
        Totals
      </h2>
      <div className="space-y-5 px-5 py-4">
        {approved.rows.length === 0 ? (
          <p className="text-center text-xs text-[#6B7682] dark:text-white/40">
            No approved entries this month.
          </p>
        ) : (
          <BreakdownSection
            title="Approved"
            rows={approved.rows}
            days={approved.dayCount}
            businessDays={businessDaysInMonth}
          />
        )}

        {notApproved.rows.length > 0 && (
          <BreakdownSection
            title="Not approved yet"
            rows={notApproved.rows}
            days={notApproved.dayCount}
            businessDays={businessDaysInMonth}
          />
        )}
      </div>
    </section>
  );
}

interface TimesheetListRow {
  key: string;
  customer: string;
  contract: string;
  status: 'Waiting for approval' | 'Approved';
  canDownload: boolean;
}

function buildTimesheetRows(monthData: TimesheetMonth): TimesheetListRow[] {
  const contractMap = new Map<string, { customer: string; contract: string; weekStatuses: Set<string> }>();
  let hasLeaveOnlyWeek = false;
  const leaveOnlyStatuses = new Set<string>();

  for (const week of monthData.weeks) {
    if (week.status !== 'Submitted' && week.status !== 'Approved') continue;

    let hasTimeEntries = false;
    for (const day of week.days) {
      for (const entry of day.timeEntries) {
        hasTimeEntries = true;
        const customer = entry.customerName || '—';
        const contract = entry.contractName || '';
        const key = `${customer}|${contract}`;
        const existing = contractMap.get(key);
        if (existing) {
          existing.weekStatuses.add(week.status);
        } else {
          contractMap.set(key, { customer, contract, weekStatuses: new Set([week.status]) });
        }
      }
    }

    if (!hasTimeEntries) {
      hasLeaveOnlyWeek = true;
      leaveOnlyStatuses.add(week.status);
    }
  }

  const rows: TimesheetListRow[] = [];

  for (const [key, entry] of contractMap) {
    const allApproved = entry.weekStatuses.size === 1 && entry.weekStatuses.has('Approved');
    rows.push({
      key,
      customer: entry.customer,
      contract: entry.contract,
      status: allApproved ? 'Approved' : 'Waiting for approval',
      canDownload: true,
    });
  }

  if (hasLeaveOnlyWeek) {
    const allApproved = leaveOnlyStatuses.size === 1 && leaveOnlyStatuses.has('Approved');
    rows.push({
      key: 'leave-only',
      customer: '—',
      contract: '',
      status: allApproved ? 'Approved' : 'Waiting for approval',
      canDownload: false,
    });
  }

  return rows;
}

function TimesheetsListCard({ monthData }: { monthData: TimesheetMonth | null }) {
  const rows = monthData ? buildTimesheetRows(monthData) : [];
  const yearStr = monthData ? String(monthData.year) : '';
  const monthStr = monthData ? String(monthData.month) : '';

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="border-b border-black/[0.06] px-5 py-4 text-base font-semibold dark:border-white/[0.06]">
        Timesheets
      </h2>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Customer</TableHead>
            <TableHead>Contract</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="w-[60px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4} className="py-8 text-center text-sm text-[#6B7682] dark:text-white/40">
                No submitted timesheets this month.
              </TableCell>
            </TableRow>
          ) : (
            rows.map((r) => (
              <TableRow key={r.key}>
                <TableCell>{r.customer}</TableCell>
                <TableCell>{r.contract}</TableCell>
                <TableCell>{r.status}</TableCell>
                <TableCell>
                  {r.canDownload && (
                    <Button variant="ghost" size="sm" asChild className="h-7 px-2">
                      <Link
                        to="/timesheets/print/$year/$month"
                        params={{ year: yearStr, month: monthStr }}
                        search={{ customer: r.customer, contract: r.contract }}
                      >
                        <FileDown className="size-4" strokeWidth={1.75} />
                        <span className="sr-only">Download PDF</span>
                      </Link>
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </section>
  );
}
