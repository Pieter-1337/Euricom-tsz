import { Link } from '@tanstack/react-router';
import { cn } from '#/lib/utils';
import { Tooltip, TooltipProvider } from '#/components/ui/tooltip';
import { dateToIsoWeek, formatIsoDate } from '#/features/timesheets/iso-week';
import { useLeaveColors } from '#/features/leaves/use-leave-colors';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April',
  'May', 'June', 'July', 'August',
  'September', 'October', 'November', 'December',
];

const DAY_LABELS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

interface DayCellInfo {
  isoDate: string;
  dayNumber: number;
  outside: boolean;
  isWeekend: boolean;
  isHoliday: boolean;
  holidayName: string | null;
  isToday: boolean;
  bookings: LeaveBookingForYear[];
}

function buildMonthCells(
  year: number,
  month: number,
  bookingsByDate: Map<string, LeaveBookingForYear[]>,
  holidayDates: Set<string>,
  holidayNames: Map<string, string>,
  today: string,
): DayCellInfo[] {
  const firstOfMonth = new Date(year, month - 1, 1);
  const lastOfMonth = new Date(year, month, 0);

  // Mon-first offset (getDay() returns 0=Sun, 1=Mon, ...)
  const fromOffset = (firstOfMonth.getDay() === 0 ? 7 : firstOfMonth.getDay()) - 1;
  const calendarStart = new Date(firstOfMonth);
  calendarStart.setDate(firstOfMonth.getDate() - fromOffset);

  const dayMs = 24 * 3600 * 1000;
  const daysToCover = Math.round((lastOfMonth.getTime() - calendarStart.getTime()) / dayMs) + 1;
  const totalCells = Math.ceil(daysToCover / 7) * 7;

  const cells: DayCellInfo[] = [];
  for (let i = 0; i < totalCells; i++) {
    const d = new Date(calendarStart);
    d.setDate(calendarStart.getDate() + i);
    const iso = formatIsoDate(d);
    const dow = d.getDay(); // 0=Sun, 6=Sat
    const isWeekend = dow === 0 || dow === 6;

    cells.push({
      isoDate: iso,
      dayNumber: d.getDate(),
      outside: d.getMonth() !== month - 1,
      isWeekend,
      isHoliday: holidayDates.has(iso),
      holidayName: holidayNames.get(iso) ?? null,
      isToday: iso === today,
      bookings: bookingsByDate.get(iso) ?? [],
    });
  }
  return cells;
}

interface DaySegment {
  key: string;
  bg: string;
  heightPct: number;
}

function buildSegments(
  bookings: LeaveBookingForYear[],
  colorMap: Map<string, { bg: string; text: string }>,
): DaySegment[] {
  // Deterministic order; the cell is split into equal bands, one per entry
  // (1 entry fills the block, 2 → halves, 3 → thirds, …).
  const sorted = [...bookings].sort((a, b) => a.leaveTypeId.localeCompare(b.leaveTypeId));
  return sorted.map((b, i) => ({
    key: `${b.leaveTypeId}-${i}`,
    bg: colorMap.get(b.leaveTypeId)?.bg ?? 'bg-amber-500/80',
    heightPct: 100 / sorted.length,
  }));
}

function DayCellContent({
  cell,
  colorMap,
}: {
  cell: DayCellInfo;
  colorMap: Map<string, { bg: string; text: string }>;
}) {
  const segments = buildSegments(cell.bookings, colorMap);
  const hasBookings = segments.length > 0;
  const { year: weekYear, week } = dateToIsoWeek(new Date(cell.isoDate + 'T00:00:00'));

  const cellContent = (
    <div
      className={cn(
        'relative flex h-full min-h-[32px] flex-col overflow-hidden rounded-sm',
        cell.isToday && 'ring-2 ring-inset ring-[#00FF00]',
        cell.outside && 'opacity-30',
        cell.isWeekend && !hasBookings && 'bg-black/[0.04] dark:bg-white/[0.04]',
        cell.isHoliday && !hasBookings && 'bg-[#3A4651]/20 dark:bg-white/[0.08]',
      )}
    >
      <span
        className={cn(
          'absolute top-0.5 right-0.5 z-10 text-[9px] leading-none',
          cell.isToday ? 'font-bold text-[#00FF00]' : 'text-[#6B7682] dark:text-white/40',
        )}
      >
        {cell.dayNumber}
      </span>

      {segments.map((s) => (
        <div key={s.key} className={cn('w-full', s.bg)} style={{ height: `${s.heightPct}%` }} />
      ))}
    </div>
  );

  const tooltipLines: string[] = cell.bookings.map(
    (b) => `${b.leaveTypeName} (${b.durationHours}h)`,
  );
  if (cell.isHoliday) tooltipLines.push(cell.holidayName ?? 'Holiday');

  // Cells are clickable — navigate to the week containing this date
  const link = (
    <Link
      to="/time-entry/week/$year/$week"
      params={{ year: String(weekYear), week: String(week) }}
      className={cn(
        'block h-full cursor-pointer rounded-sm',
        'focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#00FF00]',
      )}
      aria-label={`Week ${week} of ${weekYear}`}
    >
      {cellContent}
    </Link>
  );

  if (tooltipLines.length === 0) return link;

  return (
    <Tooltip
      content={
        <div className="flex flex-col gap-0.5">
          {tooltipLines.map((line, i) => (
            <span key={i}>{line}</span>
          ))}
        </div>
      }
    >
      {link}
    </Tooltip>
  );
}

function MonthGrid({
  year,
  month,
  bookingsByDate,
  holidayDates,
  holidayNames,
  today,
  colorMap,
}: {
  year: number;
  month: number;
  bookingsByDate: Map<string, LeaveBookingForYear[]>;
  holidayDates: Set<string>;
  holidayNames: Map<string, string>;
  today: string;
  colorMap: Map<string, { bg: string; text: string }>;
}) {
  const cells = buildMonthCells(year, month, bookingsByDate, holidayDates, holidayNames, today);

  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="border-b border-black/[0.06] px-3 py-2 text-[12px] font-semibold dark:border-white/[0.06]">
        {MONTH_NAMES[month - 1]}
      </h2>
      <div className="grid grid-cols-7 border-b border-black/[0.06] dark:border-white/[0.06]">
        {DAY_LABELS.map((d) => (
          <div
            key={d}
            className="px-1 py-1 text-center text-[9px] font-medium uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40"
          >
            {d}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-px p-1">
        {cells.map((cell) => (
          <DayCellContent
            key={cell.isoDate}
            cell={cell}
            colorMap={colorMap}
          />
        ))}
      </div>
    </section>
  );
}

export interface LeaveYearGridProps {
  year: number;
  bookings: LeaveBookingForYear[];
  holidays: HolidayDto[];
}

export function LeaveYearGrid({ year, bookings, holidays }: LeaveYearGridProps) {
  const today = formatIsoDate(new Date());

  // Build lookup maps
  const bookingsByDate = new Map<string, LeaveBookingForYear[]>();
  for (const b of bookings) {
    const existing = bookingsByDate.get(b.date) ?? [];
    existing.push(b);
    bookingsByDate.set(b.date, existing);
  }

  const holidayDates = new Set<string>(holidays.map((h) => h.date));
  const holidayNames = new Map<string, string>(holidays.map((h) => [h.date, h.name]));

  // Build color map for all unique leave type ids
  const leaveTypeIds = [...new Set(bookings.map((b) => b.leaveTypeId))];
  const colorMap = useLeaveColors(leaveTypeIds);

  return (
    <TooltipProvider delayDuration={100} skipDelayDuration={200}>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
        {Array.from({ length: 12 }, (_, i) => i + 1).map((month) => (
          <MonthGrid
            key={month}
            year={year}
            month={month}
            bookingsByDate={bookingsByDate}
            holidayDates={holidayDates}
            holidayNames={holidayNames}
            today={today}
            colorMap={colorMap}
          />
        ))}
      </div>
    </TooltipProvider>
  );
}
