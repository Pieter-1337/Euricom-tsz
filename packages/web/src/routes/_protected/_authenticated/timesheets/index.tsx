import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, FileDown } from 'lucide-react';
import { useState } from 'react';
import { Button } from '#/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { Tooltip, TooltipProvider } from '#/components/ui/tooltip';
import { InfoTooltip } from '#/components/ui/info-tooltip';
import { cn } from '#/lib/utils';
import { getLeaveColor } from '#/features/leaves/use-leave-colors';
import { fetchTimesheetMonth } from '#/features/timesheets/server-fns';
import { fetchHolidaysForYear } from '#/features/leaves/server-fns';
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

  const { data: holidays } = useQuery({
    queryKey: ['holidays', year],
    queryFn: () => fetchHolidaysForYear({ data: { year } }),
  });

  // Draft (saved-but-not-submitted) weeks are omitted from the overview and
  // its totals; those days then surface under "Not submitted yet".
  const viewMonthData = monthData
    ? { ...monthData, weeks: monthData.weeks.filter((w) => w.status !== 'Draft') }
    : null;

  return (
    <TooltipProvider delayDuration={100} skipDelayDuration={200}>
      <main className="space-y-4">
        <h1 className="text-2xl font-bold">Timesheets</h1>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[2fr_1fr]">
          <CalendarCard
            year={year}
            month={month}
            monthData={viewMonthData}
            onPrev={() => setYM(prevMonth(year, month))}
            onNext={() => setYM(nextMonth(year, month))}
            onToday={() => setYM(todayMonth())}
          />
          <TotalsCard monthData={viewMonthData} holidayDates={(holidays ?? []).map((h) => h.date)} />
        </div>

        <TimesheetsListCard monthData={viewMonthData} />
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

function buildBreakdownRows(weeks: TimesheetMonthWeek[]): BreakdownRow[] {
  const workedHours = new Map<string, number>();
  const leaveHours = new Map<string, { name: string; hours: number }>();

  for (const week of weeks) {
    for (const day of week.days) {
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

  return [
    ...Array.from(workedHours, ([name, hours]) => ({ key: `c-${name}`, label: name, hours, dot: 'bg-green-600' })),
    ...Array.from(leaveHours, ([id, v]) => ({ key: `l-${id}`, label: v.name, hours: v.hours, dot: getLeaveColor(id).bg })),
  ].sort((a, b) => b.hours - a.hours);
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

function formatNoEntryDate(iso: string): string {
  const d = new Date(iso + 'T00:00:00');
  const weekday = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'][d.getDay()];
  return `${weekday} ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
}

function TotalsCard({ monthData, holidayDates }: { monthData: TimesheetMonth | null; holidayDates: string[] }) {
  if (!monthData) {
    return (
      <section className="rounded-[12px] border border-black/[0.08] bg-white p-5 dark:border-white/[0.06] dark:bg-[#1D252D]">
        <h2 className="text-base font-semibold">Totals</h2>
      </section>
    );
  }

  const { year, month } = monthData;
  const approvedRows = buildBreakdownRows(monthData.weeks.filter((w) => w.status === 'Approved'));
  const notApprovedRows = buildBreakdownRows(monthData.weeks.filter((w) => w.status !== 'Approved'));

  // Which dates have any booking, and under which approval bucket.
  const approvedDates = new Set<string>();
  const bookedDates = new Set<string>();
  for (const week of monthData.weeks) {
    for (const day of week.days) {
      if (day.timeEntries.length === 0 && day.leaveBookings.length === 0) continue;
      bookedDates.add(day.date);
      if (week.status === 'Approved') approvedDates.add(day.date);
    }
  }

  // Enumerate the full month's workdays (Mon–Fri, excluding holidays) so the
  // denominator and the empty-day list cover untouched weeks too.
  const holidaySet = new Set(holidayDates);
  const daysInMonth = new Date(year, month, 0).getDate();
  let approvedDays = 0;
  let notApprovedDays = 0;
  const emptyDates: string[] = [];
  for (let d = 1; d <= daysInMonth; d++) {
    const date = new Date(year, month - 1, d);
    const dow = date.getDay();
    if (dow === 0 || dow === 6) continue;
    const iso = formatIsoDate(date);
    if (holidaySet.has(iso)) continue;
    if (approvedDates.has(iso)) approvedDays++;
    else if (bookedDates.has(iso)) notApprovedDays++;
    else emptyDates.push(iso);
  }
  const totalWorkdays = approvedDays + notApprovedDays + emptyDates.length;

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="border-b border-black/[0.06] px-5 py-4 text-base font-semibold dark:border-white/[0.06]">
        Totals
      </h2>
      <div className="space-y-5 px-5 py-4">
        <BreakdownSection title="Approved" rows={approvedRows} days={approvedDays} businessDays={totalWorkdays} />

        <BreakdownSection
          title="Not approved yet"
          rows={notApprovedRows}
          days={notApprovedDays}
          businessDays={totalWorkdays}
        />

        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-[#6B7682] dark:text-white/40">
            Not submitted yet
          </p>
          <div className="mt-2 border-b border-black/[0.06] pb-3 text-center text-sm font-semibold dark:border-white/[0.06]">
            {emptyDates.length} / {totalWorkdays} workdays
          </div>
          {emptyDates.length > 0 && (
            <div className="flex flex-wrap gap-1.5 pt-3">
              {emptyDates.map((iso) => (
                <span
                  key={iso}
                  className="rounded-sm bg-black/[0.04] px-1.5 py-0.5 text-xs text-[#6B7682] dark:bg-white/[0.06] dark:text-white/60"
                >
                  {formatNoEntryDate(iso)}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

interface TimesheetListRow {
  key: string;
  customer: string;
  contract: string;
  canDownload: boolean;
}

function buildTimesheetRows(monthData: TimesheetMonth): TimesheetListRow[] {
  const contractMap = new Map<string, { customer: string; contract: string }>();
  let hasLeaveOnlyWeek = false;

  for (const week of monthData.weeks) {
    if (week.status !== 'Submitted' && week.status !== 'Approved') continue;

    let hasTimeEntries = false;
    for (const day of week.days) {
      for (const entry of day.timeEntries) {
        hasTimeEntries = true;
        const customer = entry.customerName || '—';
        const contract = entry.contractName || '';
        const key = `${customer}|${contract}`;
        if (!contractMap.has(key)) {
          contractMap.set(key, { customer, contract });
        }
      }
    }

    if (!hasTimeEntries) {
      hasLeaveOnlyWeek = true;
    }
  }

  const rows: TimesheetListRow[] = [];

  for (const [key, entry] of contractMap) {
    rows.push({ key, customer: entry.customer, contract: entry.contract, canDownload: true });
  }

  if (hasLeaveOnlyWeek) {
    rows.push({ key: 'leave-only', customer: '—', contract: '', canDownload: false });
  }

  return rows;
}

function TimesheetsListCard({ monthData }: { monthData: TimesheetMonth | null }) {
  const rows = monthData ? buildTimesheetRows(monthData) : [];
  const yearStr = monthData ? String(monthData.year) : '';
  const monthStr = monthData ? String(monthData.month) : '';

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="flex items-center gap-2 border-b border-black/[0.06] px-5 py-4 text-base font-semibold dark:border-white/[0.06]">
        Timesheets
        <InfoTooltip
          content="Document will only contain approved entries that are relevant for the customer"
          className="cursor-pointer"
        />
      </h2>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Customer</TableHead>
            <TableHead>Contract</TableHead>
            <TableHead className="w-[60px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={3} className="py-8 text-center text-sm text-[#6B7682] dark:text-white/40">
                No submitted timesheets this month.
              </TableCell>
            </TableRow>
          ) : (
            rows.map((r) => (
              <TableRow key={r.key}>
                <TableCell>{r.customer}</TableCell>
                <TableCell>{r.contract}</TableCell>
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
