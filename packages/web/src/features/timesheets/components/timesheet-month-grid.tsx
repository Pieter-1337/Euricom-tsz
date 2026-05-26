import { useNavigate } from '@tanstack/react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { cn } from '#/lib/utils';
import type { TimesheetMonth, TimesheetMonthWeek } from '#/api/timesheets';
import { dateToIsoWeek, formatMonthLabel, nextMonth, prevMonth, todayMonth } from '#/features/timesheets/iso-week';

const DAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function formatShortDate(isoDate: string): string {
  const d = new Date(isoDate + 'T00:00:00');
  return `${d.getDate()} ${MONTH_NAMES[d.getMonth()]}`;
}

function weekStatusClass(status: string): string {
  if (status === 'Approved') return 'bg-green-100 dark:bg-green-900/20 border-green-200 dark:border-green-800';
  if (status === 'Submitted') return 'bg-green-50 dark:bg-green-950/20 border-green-100 dark:border-green-900';
  return 'bg-white dark:bg-[#1D252D] border-black/[0.08] dark:border-white/[0.06]';
}

function weekStatusBadge(status: string): { label: string; cls: string } | null {
  if (status === 'Approved') return { label: 'Approved', cls: 'bg-green-600 text-white' };
  if (status === 'Submitted')
    return { label: 'Submitted', cls: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300' };
  return null;
}

interface TimesheetMonthGridProps {
  month: TimesheetMonth;
  year: number;
  monthNum: number;
}

function WeekSection({ week }: { week: TimesheetMonthWeek }) {
  const navigate = useNavigate();
  const badge = weekStatusBadge(week.status);

  const handleDayClick = async (isoDate: string) => {
    const d = new Date(isoDate + 'T00:00:00');
    const { year, week: isoWeek } = dateToIsoWeek(d);
    await navigate({ to: '/time-entry/week/$year/$week', params: { year: String(year), week: String(isoWeek) } });
  };

  return (
    <div className={cn('rounded-lg border overflow-hidden mb-4', weekStatusClass(week.status))}>
      {/* Week header */}
      <div className="flex items-center justify-between px-4 py-2 border-b border-black/[0.06] dark:border-white/[0.06] bg-[#F1F5F6]/60 dark:bg-[#232C35]/60">
        <span className="text-xs font-semibold uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40">
          Week {week.isoWeek} · {week.isoYear}
        </span>
        {badge && (
          <span className={cn('inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold', badge.cls)}>
            {badge.label}
          </span>
        )}
      </div>

      {/* Day cells */}
      <div className="grid grid-cols-7 gap-0">
        {DAY_LABELS.map((label) => (
          <div
            key={label}
            className="px-2 py-1 text-center text-[10px] font-medium uppercase tracking-[0.18em] text-[#6B7682] dark:text-white/40 border-b border-black/[0.04] dark:border-white/[0.04]"
          >
            {label}
          </div>
        ))}
        {week.days.map((day) => {
          const shortDate = formatShortDate(day.date);
          const isWeekend =
            !day.isBusinessDay &&
            (new Date(day.date + 'T00:00:00').getDay() === 0 || new Date(day.date + 'T00:00:00').getDay() === 6);

          return (
            <button
              key={day.date}
              className={cn(
                'flex flex-col items-center px-1 py-2 text-center transition-colors duration-[120ms] min-h-[64px]',
                'border-r border-b border-black/[0.04] dark:border-white/[0.04] last:border-r-0',
                day.isBusinessDay
                  ? 'hover:bg-black/[0.04] dark:hover:bg-white/[0.04] cursor-pointer'
                  : 'opacity-40 cursor-default pointer-events-none',
              )}
              onClick={() => void handleDayClick(day.date)}
              disabled={!day.isBusinessDay}
              title={day.isBusinessDay ? `Go to week for ${shortDate}` : shortDate}
            >
              <span className="text-[11px] font-medium text-[#6B7682] dark:text-white/50 mb-1">{shortDate}</span>
              {day.totalHours > 0 && (
                <span className="text-sm font-bold text-[#3A4651] dark:text-white/90">{day.totalHours}</span>
              )}
              {!day.isBusinessDay && !isWeekend && (
                <span className="text-[9px] text-[#6B7682]/60 dark:text-white/30 mt-0.5">holiday</span>
              )}
            </button>
          );
        })}
      </div>

      {/* Summaries */}
      {(week.perTaskSummary.length > 0 || week.perLeaveTypeSummary.length > 0) && (
        <div className="flex gap-6 px-4 py-3 border-t border-black/[0.06] dark:border-white/[0.06] flex-wrap">
          {week.perTaskSummary.length > 0 && (
            <div className="flex-1 min-w-[160px]">
              <div className="text-[10px] font-semibold uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40 mb-1">
                By task
              </div>
              {week.perTaskSummary.map((s) => (
                <div
                  key={`${s.contractName}-${s.taskName}`}
                  className="flex justify-between text-xs text-[#3A4651] dark:text-white/80 py-0.5"
                >
                  <span className="truncate max-w-[180px]" title={`${s.contractName} — ${s.taskName}`}>
                    {s.taskName}
                  </span>
                  <span className="ml-4 font-semibold shrink-0">{s.totalHours}h</span>
                </div>
              ))}
            </div>
          )}
          {week.perLeaveTypeSummary.length > 0 && (
            <div className="flex-1 min-w-[120px]">
              <div className="text-[10px] font-semibold uppercase tracking-[0.2em] text-amber-600 dark:text-amber-400/60 mb-1">
                By leave
              </div>
              {week.perLeaveTypeSummary.map((s) => (
                <div
                  key={s.leaveTypeName}
                  className="flex justify-between text-xs text-amber-700 dark:text-amber-400 py-0.5"
                >
                  <span>{s.leaveTypeName}</span>
                  <span className="ml-4 font-semibold">{s.totalHours}h</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function TimesheetMonthGrid({ month, year, monthNum }: TimesheetMonthGridProps) {
  const navigate = useNavigate();
  const { year: prevY, month: prevM } = prevMonth(year, monthNum);
  const { year: nextY, month: nextM } = nextMonth(year, monthNum);
  const { year: todayY, month: todayM } = todayMonth();

  const navigateToMonth = async (y: number, m: number) => {
    await navigate({ to: '/time-entry/month/$year/$month', params: { year: String(y), month: String(m) } });
  };

  return (
    <div className="flex flex-col gap-4">
      {/* Month navigation header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void navigateToMonth(prevY, prevM)}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="min-w-[160px] text-center text-sm font-semibold">{formatMonthLabel(year, monthNum)}</span>
          <Button variant="outline" size="sm" onClick={() => void navigateToMonth(nextY, nextM)}>
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="ml-1 text-xs"
            onClick={() => void navigateToMonth(todayY, todayM)}
          >
            Today
          </Button>
        </div>
        <div className="text-sm text-[#6B7682] dark:text-white/40">
          {month.monthTotalHours > 0 && (
            <span className="font-semibold text-[#3A4651] dark:text-white/80">{month.monthTotalHours}h total</span>
          )}
        </div>
      </div>

      {/* Legend */}
      <div className="flex items-center gap-4 text-xs text-[#6B7682] dark:text-white/40">
        <div className="flex items-center gap-1.5">
          <div className="h-3 w-3 rounded bg-green-50 dark:bg-green-950/20 border border-green-100 dark:border-green-900" />
          <span>Submitted</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="h-3 w-3 rounded bg-green-100 dark:bg-green-900/20 border border-green-200 dark:border-green-800" />
          <span>Approved</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="h-3 w-3 rounded bg-white dark:bg-[#1D252D] border border-black/[0.08] dark:border-white/[0.06]" />
          <span>Draft</span>
        </div>
      </div>

      {/* Weeks */}
      {month.weeks.length === 0 ? (
        <div className="rounded-lg border border-black/[0.08] dark:border-white/[0.06] px-6 py-10 text-center text-sm text-[#6B7682] dark:text-white/40">
          No timesheet data for this month.
        </div>
      ) : (
        month.weeks.map((week) => <WeekSection key={`${week.isoYear}-${week.isoWeek}`} week={week} />)
      )}

      {/* Monthly summary panel */}
      {month.weeks.length > 0 && (
        <div className="rounded-lg border border-black/[0.08] dark:border-white/[0.06] px-5 py-4 bg-[#F1F5F6]/50 dark:bg-[#1D252D]">
          <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6B7682] dark:text-white/40 mb-3">
            Month summary
          </div>
          <div className="flex gap-8 flex-wrap">
            {/* Per-task totals across all weeks */}
            {(() => {
              const byTask = new Map<string, { contractName: string; taskName: string; total: number }>();
              month.weeks.forEach((w) =>
                w.perTaskSummary.forEach((s) => {
                  const key = `${s.contractName}:${s.taskName}`;
                  const existing = byTask.get(key);
                  if (existing) existing.total += s.totalHours;
                  else byTask.set(key, { contractName: s.contractName, taskName: s.taskName, total: s.totalHours });
                }),
              );
              const entries = Array.from(byTask.values());
              if (entries.length === 0) return null;
              return (
                <div className="flex-1 min-w-[160px]">
                  <div className="text-[10px] font-semibold uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40 mb-1">
                    By task
                  </div>
                  {entries.map((e) => (
                    <div
                      key={`${e.contractName}-${e.taskName}`}
                      className="flex justify-between text-xs text-[#3A4651] dark:text-white/80 py-0.5"
                    >
                      <span className="truncate max-w-[180px]" title={`${e.contractName} — ${e.taskName}`}>
                        {e.taskName}
                      </span>
                      <span className="ml-4 font-semibold shrink-0">{e.total}h</span>
                    </div>
                  ))}
                </div>
              );
            })()}

            {/* Per-leave-type totals */}
            {(() => {
              const byLeave = new Map<string, number>();
              month.weeks.forEach((w) =>
                w.perLeaveTypeSummary.forEach((s) => {
                  byLeave.set(s.leaveTypeName, (byLeave.get(s.leaveTypeName) ?? 0) + s.totalHours);
                }),
              );
              const entries = Array.from(byLeave.entries());
              if (entries.length === 0) return null;
              return (
                <div className="flex-1 min-w-[120px]">
                  <div className="text-[10px] font-semibold uppercase tracking-[0.2em] text-amber-600 dark:text-amber-400/60 mb-1">
                    By leave
                  </div>
                  {entries.map(([name, total]) => (
                    <div key={name} className="flex justify-between text-xs text-amber-700 dark:text-amber-400 py-0.5">
                      <span>{name}</span>
                      <span className="ml-4 font-semibold">{total}h</span>
                    </div>
                  ))}
                </div>
              );
            })()}
          </div>
        </div>
      )}
    </div>
  );
}
