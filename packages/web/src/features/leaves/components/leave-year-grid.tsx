import { Link } from '@tanstack/react-router';
import { cn } from '#/lib/utils';
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

interface BookingDisplay {
  leaveTypeId: string;
  leaveTypeName: string;
  isHalfDay: boolean;
}

function classifyDay(bookings: LeaveBookingForYear[]): {
  type: 'none' | 'single' | 'split' | 'multi';
  displays: BookingDisplay[];
} {
  if (bookings.length === 0) return { type: 'none', displays: [] };

  // Sort by (leaveTypeId) for deterministic ordering
  const sorted = [...bookings].sort((a, b) => a.leaveTypeId.localeCompare(b.leaveTypeId));

  // De-duplicate by leaveTypeId, keeping first booking per type
  const seen = new Set<string>();
  const unique: BookingDisplay[] = [];
  for (const b of sorted) {
    if (!seen.has(b.leaveTypeId)) {
      seen.add(b.leaveTypeId);
      unique.push({
        leaveTypeId: b.leaveTypeId,
        leaveTypeName: b.leaveTypeName,
        isHalfDay: b.durationHours < 8,
      });
    }
  }

  if (unique.length === 1) return { type: 'single', displays: unique };
  if (unique.length === 2) return { type: 'split', displays: unique };
  return { type: 'multi', displays: unique };
}

function DayCellContent({
  cell,
  colorMap,
}: {
  cell: DayCellInfo;
  colorMap: Map<string, { bg: string; text: string }>;
}) {
  const { type, displays } = classifyDay(cell.bookings);
  const { year: weekYear, week } = dateToIsoWeek(new Date(cell.isoDate + 'T00:00:00'));

  const cellContent = (
    <div
      className={cn(
        'relative flex h-full min-h-[32px] flex-col overflow-hidden rounded-sm',
        cell.isToday && 'ring-2 ring-inset ring-[#00FF00]',
        cell.outside && 'opacity-30',
        cell.isWeekend && !type && 'bg-black/[0.04] dark:bg-white/[0.04]',
        cell.isHoliday && type === 'none' && 'bg-[#3A4651]/20 dark:bg-white/[0.08]',
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

      {type === 'single' && displays[0] && (
        <div
          className={cn(
            'h-full w-full',
            colorMap.get(displays[0].leaveTypeId)?.bg ?? 'bg-amber-500/80',
          )}
        >
          {displays[0].isHalfDay && (
            <div className="h-1/2 w-full bg-white/30" />
          )}
        </div>
      )}

      {type === 'split' && displays[0] && displays[1] && (
        <>
          <div
            className={cn(
              'h-1/2 w-full',
              colorMap.get(displays[0].leaveTypeId)?.bg ?? 'bg-amber-500/80',
            )}
          />
          <div
            className={cn(
              'h-1/2 w-full',
              colorMap.get(displays[1].leaveTypeId)?.bg ?? 'bg-blue-500/80',
            )}
          />
        </>
      )}

      {type === 'multi' && (
        <div
          className="h-full w-full"
          style={{
            backgroundImage: 'repeating-linear-gradient(45deg, rgba(100,100,100,0.3) 0px, rgba(100,100,100,0.3) 3px, transparent 3px, transparent 9px)',
          }}
        />
      )}
    </div>
  );

  // Cells are clickable — navigate to the week containing this date
  return (
    <Link
      to="/time-entry/week/$year/$week"
      params={{ year: String(weekYear), week: String(week) }}
      title={cell.isHoliday ? (cell.holidayName ?? 'Holiday') : undefined}
      className={cn(
        'block h-full focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#00FF00]',
        'rounded-sm',
      )}
      aria-label={`Week ${week} of ${weekYear}`}
    >
      {cellContent}
    </Link>
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
  );
}
