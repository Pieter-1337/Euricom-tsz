import type { TimesheetMonth, TimesheetMonthDay } from '#/api/timesheets';

interface TimesheetPrintDocProps {
  monthData: TimesheetMonth;
  year: number;
  month: number;
  customer: string;
  contract: string;
  consultantName: string;
}

interface PrintDay {
  isoDate: string;
  dayLabel: string;
  ddmm: string;
  isWeekend: boolean;
  entries: { taskName: string; hours: number; days: number }[];
}

const FULL_MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

const WEEKDAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function buildPrintDays(
  monthData: TimesheetMonth,
  year: number,
  month: number,
  customer: string,
  contract: string,
): PrintDay[] {
  const prefix = `${year}-${String(month).padStart(2, '0')}`;

  const dayMap = new Map<string, TimesheetMonthDay>();
  for (const week of monthData.weeks) {
    for (const day of week.days) {
      if (day.date.startsWith(prefix) && !dayMap.has(day.date)) {
        dayMap.set(day.date, day);
      }
    }
  }

  const sorted = Array.from(dayMap.values()).sort((a, b) => a.date.localeCompare(b.date));

  return sorted.map((day) => {
    const d = new Date(day.date + 'T00:00:00');
    const dow = d.getDay();
    const isWeekend = dow === 0 || dow === 6;

    const matchingEntries = day.timeEntries.filter(
      (e) => e.customerName === customer && e.contractName === contract,
    );

    const entries = matchingEntries.map((e) => ({
      taskName: e.taskName,
      hours: e.durationHours,
      days: e.durationHours / 8,
    }));

    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');

    return {
      isoDate: day.date,
      dayLabel: WEEKDAY_NAMES[dow],
      ddmm: `${dd}/${mm}`,
      isWeekend,
      entries,
    };
  });
}

export function TimesheetPrintDoc({
  monthData,
  year,
  month,
  customer,
  contract,
  consultantName,
}: TimesheetPrintDocProps) {
  const printDays = buildPrintDays(monthData, year, month, customer, contract);

  const totalDays = printDays.reduce(
    (sum, d) => sum + d.entries.reduce((s, e) => s + e.days, 0),
    0,
  );
  const totalHours = printDays.reduce(
    (sum, d) => sum + d.entries.reduce((s, e) => s + e.hours, 0),
    0,
  );

  const monthLabel = `${FULL_MONTH_NAMES[month - 1]} ${year}`;

  return (
    <div
      className="[print-color-adjust:exact] bg-white text-black"
      style={{ fontFamily: 'Montserrat, sans-serif', minWidth: 640 }}
    >
      {/* Header */}
      <div className="mb-6 flex items-start justify-between">
        {/* Brand */}
        <div>
          <BrandmarkSvg className="h-8 w-8" />
          <span
            className="mt-1 block text-[10px] font-semibold uppercase tracking-[0.3em] text-[#1D252D]"
          >
            euricom
          </span>
        </div>

        {/* Label/value grid */}
        <div className="text-right text-[11px]">
          <LabelValue label="Consultant" value={consultantName} />
          <LabelValue label="Contract" value={contract} />
          <LabelValue label="Customer" value={customer} />
          <LabelValue label="Customer ref." value="" />
        </div>
      </div>

      {/* Centered title */}
      <h1 className="mb-6 text-center text-xl font-bold tracking-tight text-[#1D252D]">
        Timesheet {monthLabel}
      </h1>

      {/* Table */}
      <table
        className="w-full border-collapse text-[11px]"
        style={{ borderCollapse: 'collapse' }}
      >
        <thead>
          <tr>
            <th
              className="border border-[#ccc] px-3 py-2 text-left font-semibold text-white"
              style={{ backgroundColor: '#2d7a2d', width: '22%' }}
            >
              Date
            </th>
            <th
              className="border border-[#ccc] px-3 py-2 text-left font-semibold text-white"
              style={{ backgroundColor: '#2d7a2d' }}
            >
              Task
            </th>
            <th
              className="border border-[#ccc] px-3 py-2 text-right font-semibold text-white"
              style={{ backgroundColor: '#2d7a2d', width: '12%' }}
            >
              Hours
            </th>
            <th
              className="border border-[#ccc] px-3 py-2 text-right font-semibold text-white"
              style={{ backgroundColor: '#2d7a2d', width: '12%' }}
            >
              Days
            </th>
          </tr>
        </thead>
        <tbody>
          {printDays.map((day) => {
            if (day.isWeekend || day.entries.length === 0) {
              return (
                <tr
                  key={day.isoDate}
                  style={{ backgroundColor: day.isWeekend ? '#f0f0f0' : undefined }}
                >
                  <td className="border border-[#ccc] px-3 py-1.5 text-[#555]">
                    {day.dayLabel} {day.ddmm}
                  </td>
                  <td className="border border-[#ccc] px-3 py-1.5" />
                  <td className="border border-[#ccc] px-3 py-1.5" />
                  <td className="border border-[#ccc] px-3 py-1.5" />
                </tr>
              );
            }

            return day.entries.map((entry, i) => (
              <tr key={`${day.isoDate}-${i}`}>
                <td className="border border-[#ccc] px-3 py-1.5">
                  {i === 0 ? `${day.dayLabel} ${day.ddmm}` : ''}
                </td>
                <td className="border border-[#ccc] px-3 py-1.5">{entry.taskName}</td>
                <td className="border border-[#ccc] px-3 py-1.5 text-right">
                  {entry.hours.toFixed(2)}
                </td>
                <td className="border border-[#ccc] px-3 py-1.5 text-right">
                  {entry.days.toFixed(2)}
                </td>
              </tr>
            ));
          })}

          {/* Footer total row */}
          <tr>
            <td
              className="border border-[#ccc] px-3 py-2 font-bold"
              colSpan={2}
            >
              TOTAL
            </td>
            <td className="border border-[#ccc] px-3 py-2 text-right font-bold">
              {totalHours.toFixed(2)}
            </td>
            <td className="border border-[#ccc] px-3 py-2 text-right font-bold">
              {totalDays.toFixed(2)}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}

function LabelValue({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-end gap-3">
      <span className="font-semibold text-[#555]">{label}:</span>
      <span className="min-w-[120px] text-left">{value}</span>
    </div>
  );
}

function BrandmarkSvg({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 256 256"
      className={className}
      style={{ fill: '#00FF00' }}
      aria-label="Euricom"
      role="img"
    >
      <path d="M39.981 39.9826H109.998V0H35.1838C15.7526 0 0 15.7532 0 35.1852V110.003H39.9675V39.9826H39.981Z" />
      <path d="M156.702 128.008C156.702 143.856 143.86 156.711 128 156.711C112.14 156.711 99.2979 143.869 99.2979 128.008C99.2979 112.147 112.153 99.3047 128 99.3047C143.847 99.3047 156.702 112.147 156.702 128.008Z" />
      <path d="M220.809 0H145.994V39.9691H216.011V109.989H255.979V35.1852C255.979 15.7532 240.226 0 220.795 0" />
      <path d="M216.019 145.998V216.018H146.002V255.987H220.816C240.248 255.987 256 240.234 256 220.802V145.984H216.033L216.019 145.998Z" />
      <path d="M39.9832 216.016V145.996H0.015625V220.814C0.015625 240.246 15.7681 255.999 35.1994 255.999H110.014V216.03H39.9966L39.9832 216.016Z" />
    </svg>
  );
}
