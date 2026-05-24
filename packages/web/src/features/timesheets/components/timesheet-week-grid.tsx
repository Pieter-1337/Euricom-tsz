import { useCallback, useEffect, useState } from 'react';
import { useBlocker, useNavigate, useRouter } from '@tanstack/react-router';
import { Calendar, ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Calendar as CalendarPicker } from '#/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { cn } from '#/lib/utils';
import type { TimesheetWeek, SelectableContractTask, BookingInput } from '#/api/timesheets';
import {
  dateToIsoWeek,
  formatDayHeader,
  isoWeekToMonday,
  nextWeek,
  parseDurationInput,
  prevWeek,
  todayWeek,
} from '#/features/timesheets/iso-week';
import {
  submitTimesheetBookings,
  submitWeekLifecycle,
  approveWeekLifecycle,
  reopenWeekLifecycle,
} from '#/features/timesheets/server-fns';

type CellKey = `${string}:${string}`; // `${contractTaskId}:${date}`

interface TimesheetWeekGridProps {
  userId: string;
  year: number;
  week: number;
  initialData: TimesheetWeek | null;
  selectableTasks: SelectableContractTask[];
  isAdmin: boolean;
}

function cellKey(contractTaskId: string, date: string): CellKey {
  return `${contractTaskId}:${date}` as CellKey;
}

export function TimesheetWeekGrid({
  userId,
  year,
  week,
  initialData,
  selectableTasks,
  isAdmin,
}: TimesheetWeekGridProps) {
  const navigate = useNavigate();
  const router = useRouter();
  const [bookings, setBookings] = useState<Map<CellKey, number>>(() => {
    const map = new Map<CellKey, number>();
    initialData?.timeEntries?.forEach((e) => {
      map.set(cellKey(e.contractTaskId, e.date), e.durationHours);
    });
    return map;
  });

  const [taskRows, setTaskRows] = useState<Array<{ id: string; name: string }>>(() => {
    const seen = new Map<string, string>();
    initialData?.timeEntries?.forEach((e) => {
      if (!seen.has(e.contractTaskId)) seen.set(e.contractTaskId, e.taskName);
    });
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }));
  });

  const [isDirty, setIsDirty] = useState(false);
  const [isFlushing, setIsFlushing] = useState(false);
  const [isLifecycleLoading, setIsLifecycleLoading] = useState(false);
  const [showTaskPicker, setShowTaskPicker] = useState(false);
  const [showCalendar, setShowCalendar] = useState(false);
  const [editingCell, setEditingCell] = useState<CellKey | null>(null);
  const [cellInputs, setCellInputs] = useState<Map<CellKey, string>>(new Map());

  const days = initialData?.days ?? [];
  const status = initialData?.status ?? 'Draft';
  const isDraft = status === 'Draft';

  const flush = useCallback(async () => {
    if (!isDirty || isFlushing) return;
    setIsFlushing(true);
    try {
      const inputs: BookingInput[] = [];
      bookings.forEach((durationHours, key) => {
        const [contractTaskId, date] = key.split(':');
        inputs.push({ contractTaskId, date, durationHours });
      });
      await submitTimesheetBookings({
        data: { userId, year, week, bookings: inputs },
      });
      setIsDirty(false);
    } catch {
      // silently fail — data stays in local state
    } finally {
      setIsFlushing(false);
    }
  }, [isDirty, isFlushing, bookings, userId, year, week]);

  useBlocker({
    shouldBlockFn: async () => {
      await flush();
      return false;
    },
    disabled: !isDirty,
    enableBeforeUnload: false,
  });

  useEffect(() => {
    const handler = () => {
      void flush();
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [flush]);

  const setCell = useCallback(
    (taskId: string, date: string, value: number | null) => {
      if (!isDraft) return;
      setBookings((prev) => {
        const next = new Map(prev);
        const key = cellKey(taskId, date);
        if (value === null) next.delete(key);
        else next.set(key, value);
        return next;
      });
      setIsDirty(true);
    },
    [isDraft],
  );

  const handleCellKeyDown = (taskId: string, date: string, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isDraft) return;
    if (e.key === 'd') {
      e.preventDefault();
      setCell(taskId, date, 8);
      setCellInputs((prev) => new Map(prev).set(cellKey(taskId, date), '8'));
    } else if (e.key === 'h') {
      e.preventDefault();
      setCell(taskId, date, 4);
      setCellInputs((prev) => new Map(prev).set(cellKey(taskId, date), '4'));
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      if (e.key === 'Delete') {
        e.preventDefault();
        setCell(taskId, date, null);
        setCellInputs((prev) => new Map(prev).set(cellKey(taskId, date), ''));
      }
    }
  };

  const handleCellChange = (taskId: string, date: string, raw: string) => {
    const key = cellKey(taskId, date);
    setCellInputs((prev) => new Map(prev).set(key, raw));
    if (raw === '' || raw === '0') {
      setCell(taskId, date, null);
      return;
    }
    const val = parseDurationInput(raw);
    if (val !== null) setCell(taskId, date, val);
  };

  const handleCellBlur = (taskId: string, date: string) => {
    setEditingCell(null);
    const key = cellKey(taskId, date);
    const raw = cellInputs.get(key) ?? '';
    if (raw === '') {
      setCell(taskId, date, null);
    } else {
      const val = parseDurationInput(raw);
      if (val === null) {
        const existing = bookings.get(key);
        setCellInputs((prev) => {
          const next = new Map(prev);
          next.set(key, existing !== undefined ? String(existing) : '');
          return next;
        });
      }
    }
  };

  const addTaskRow = (task: SelectableContractTask) => {
    if (!taskRows.some((r) => r.id === task.contractTaskId)) {
      setTaskRows((prev) => [...prev, { id: task.contractTaskId, name: task.taskName }]);
    }
    setShowTaskPicker(false);
  };

  const handleSubmit = async () => {
    await flush();
    setIsLifecycleLoading(true);
    try {
      await submitWeekLifecycle({ data: { userId, year, week } });
      await router.invalidate();
    } catch {
      // error stays silent; future work: surface toast
    } finally {
      setIsLifecycleLoading(false);
    }
  };

  const handleApprove = async () => {
    setIsLifecycleLoading(true);
    try {
      await approveWeekLifecycle({ data: { userId, year, week } });
      await router.invalidate();
    } catch {
      // error stays silent; future work: surface toast
    } finally {
      setIsLifecycleLoading(false);
    }
  };

  const handleReopen = async () => {
    setIsLifecycleLoading(true);
    try {
      await reopenWeekLifecycle({ data: { userId, year, week } });
      await router.invalidate();
    } catch {
      // error stays silent; future work: surface toast
    } finally {
      setIsLifecycleLoading(false);
    }
  };

  const { year: prevYear, week: prevWeekNum } = prevWeek(year, week);
  const { year: nextYear, week: nextWeekNum } = nextWeek(year, week);
  const today = todayWeek();

  const navigateToWeek = async (y: number, w: number) => {
    await flush();
    await navigate({ to: '/timesheets/week/$year/$week', params: { year: String(y), week: String(w) } });
  };

  // Day totals
  const dayTotals = new Map<string, number>();
  days.forEach((d) => {
    let total = 0;
    taskRows.forEach((r) => {
      total += bookings.get(cellKey(r.id, d.date)) ?? 0;
    });
    dayTotals.set(d.date, total);
  });

  const weekTotal = Array.from(dayTotals.values()).reduce((a, b) => a + b, 0);

  const addableTaskIds = new Set(taskRows.map((r) => r.id));
  const availableToAdd = selectableTasks.filter((t) => !addableTaskIds.has(t.contractTaskId));

  return (
    <div className="flex flex-col gap-4">
      {/* Week navigation header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => void navigateToWeek(prevYear, prevWeekNum)}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="min-w-[120px] text-center text-sm font-semibold">
            Week {week}, {year}
          </span>
          <Button variant="outline" size="sm" onClick={() => void navigateToWeek(nextYear, nextWeekNum)}>
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="ml-1 text-xs"
            onClick={() => void navigateToWeek(today.year, today.week)}
          >
            Today
          </Button>
          <Popover open={showCalendar} onOpenChange={setShowCalendar}>
            <PopoverTrigger asChild>
              <Button variant="ghost" size="sm" className="ml-1" title="Pick a week">
                <Calendar className="h-4 w-4" />
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-auto p-0" align="start">
              <CalendarPicker
                mode="single"
                defaultMonth={isoWeekToMonday(year, week)}
                onSelect={(d) => {
                  if (d) {
                    const { year: y, week: w } = dateToIsoWeek(d);
                    setShowCalendar(false);
                    void navigateToWeek(y, w);
                  }
                }}
                autoFocus
              />
            </PopoverContent>
          </Popover>
        </div>

        <div className="flex items-center gap-2">
          {isDirty && <span className="text-xs text-amber-500">Unsaved changes</span>}
          {isFlushing && <span className="text-xs text-[#6B7682]">Saving...</span>}
          {isDraft && (
            <Button size="sm" disabled={isLifecycleLoading} onClick={() => void handleSubmit()}>
              Submit
            </Button>
          )}
          {isAdmin && status === 'Submitted' && (
            <Button size="sm" disabled={isLifecycleLoading} onClick={() => void handleApprove()}>
              Approve
            </Button>
          )}
          {isAdmin && (status === 'Submitted' || status === 'Approved') && (
            <Button variant="outline" size="sm" disabled={isLifecycleLoading} onClick={() => void handleReopen()}>
              Reopen
            </Button>
          )}
        </div>
      </div>

      {/* Status badge */}
      {status !== 'Draft' && (
        <div
          className={cn(
            'inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold w-fit',
            status === 'Submitted' && 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
            status === 'Approved' && 'bg-green-600 text-white',
          )}
        >
          {status}
        </div>
      )}

      {/* Grid */}
      <div
        className={cn(
          'overflow-x-auto rounded-lg border border-black/[0.08] dark:border-white/[0.06]',
          status === 'Submitted' && 'bg-green-50 dark:bg-green-950/20',
          status === 'Approved' && 'bg-green-100 dark:bg-green-900/20',
        )}
      >
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-black/[0.08] dark:border-white/[0.06] bg-[#F1F5F6] dark:bg-[#1D252D]">
              <th className="w-48 px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6B7682] dark:text-white/40">
                Task
              </th>
              {days.map((d) => {
                const { day, label } = formatDayHeader(d.date);
                return (
                  <th
                    key={d.date}
                    className={cn('min-w-[72px] px-2 py-3 text-center', !d.isBusinessDay && 'opacity-40')}
                  >
                    <div className="text-[10px] font-medium uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40">
                      {day}
                    </div>
                    <div className="text-xs font-semibold text-[#3A4651] dark:text-white/70">{label}</div>
                  </th>
                );
              })}
              <th className="min-w-[60px] px-2 py-3 text-center text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6B7682] dark:text-white/40">
                Total
              </th>
            </tr>
          </thead>
          <tbody>
            {taskRows.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-sm text-[#6B7682] dark:text-white/40">
                  No tasks added. Use the button below to add a contract task.
                </td>
              </tr>
            )}
            {taskRows.map((row) => {
              const rowTotal = days.reduce((sum, d) => sum + (bookings.get(cellKey(row.id, d.date)) ?? 0), 0);
              return (
                <tr
                  key={row.id}
                  className="border-t border-black/[0.04] dark:border-white/[0.04] hover:bg-black/[0.01] dark:hover:bg-white/[0.01]"
                >
                  <td
                    className="px-4 py-2 font-medium text-[13px] text-[#3A4651] dark:text-white/80 truncate max-w-[192px]"
                    title={row.name}
                  >
                    {row.name}
                  </td>
                  {days.map((d) => {
                    const key = cellKey(row.id, d.date);
                    const stored = bookings.get(key);
                    const isEditing = editingCell === key;
                    const rawInput = cellInputs.get(key) ?? (stored !== undefined ? String(stored) : '');
                    const isReadOnly = !isDraft || !d.isBusinessDay;

                    return (
                      <td
                        key={d.date}
                        className={cn(
                          'px-1 py-1 text-center',
                          !d.isBusinessDay && 'bg-black/[0.02] dark:bg-white/[0.01]',
                        )}
                      >
                        {isReadOnly ? (
                          <div
                            className={cn(
                              'h-8 w-full rounded text-center text-sm flex items-center justify-center',
                              stored !== undefined && 'font-semibold text-[#3A4651] dark:text-white/90',
                              stored === undefined && 'text-[#6B7682]/40',
                            )}
                          >
                            {stored !== undefined ? stored : !d.isBusinessDay ? '—' : ''}
                          </div>
                        ) : (
                          <input
                            type="text"
                            className={cn(
                              'h-8 w-16 rounded border text-center text-sm transition-colors duration-[120ms]',
                              'border-black/[0.10] bg-white dark:border-white/[0.10] dark:bg-transparent',
                              'text-[#3A4651] dark:text-white/90 placeholder:text-[#6B7682]/50',
                              'focus:outline-none focus-visible:outline-[#00FF00] focus-visible:outline-2 focus-visible:outline-offset-1',
                              stored !== undefined && 'font-semibold',
                            )}
                            value={isEditing ? rawInput : stored !== undefined ? String(stored) : ''}
                            placeholder=""
                            onFocus={() => {
                              setEditingCell(key);
                              setCellInputs((prev) =>
                                new Map(prev).set(key, stored !== undefined ? String(stored) : ''),
                              );
                            }}
                            onChange={(e) => handleCellChange(row.id, d.date, e.target.value)}
                            onBlur={() => handleCellBlur(row.id, d.date)}
                            onKeyDown={(e) => handleCellKeyDown(row.id, d.date, e)}
                          />
                        )}
                      </td>
                    );
                  })}
                  <td className="px-2 py-2 text-center font-semibold text-sm text-[#3A4651] dark:text-white/80">
                    {rowTotal > 0 ? rowTotal : ''}
                  </td>
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="border-t border-black/[0.08] dark:border-white/[0.06] bg-[#F1F5F6] dark:bg-[#1D252D]">
              <td className="px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40">
                Day total
              </td>
              {days.map((d) => {
                const total = dayTotals.get(d.date) ?? 0;
                return (
                  <td
                    key={d.date}
                    className="px-2 py-2 text-center font-semibold text-sm text-[#3A4651] dark:text-white/80"
                  >
                    {total > 0 ? total : ''}
                  </td>
                );
              })}
              <td className="px-2 py-2 text-center font-bold text-sm text-[#3A4651] dark:text-white">
                {weekTotal > 0 ? weekTotal : ''}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      {/* Add task row */}
      {isDraft && (
        <div className="relative">
          <Button variant="ghost" size="sm" className="gap-2 text-[13px]" onClick={() => setShowTaskPicker((v) => !v)}>
            <Plus className="h-4 w-4" />
            Add task row
          </Button>

          {showTaskPicker && (
            <div className="absolute left-0 top-full z-10 mt-1 w-72 rounded-lg border border-black/[0.10] bg-white shadow-md dark:border-white/[0.10] dark:bg-[#232C35]">
              {availableToAdd.length === 0 ? (
                <div className="px-4 py-3 text-sm text-[#6B7682] dark:text-white/40">
                  No more tasks available for this week.
                </div>
              ) : (
                <ul>
                  {availableToAdd.map((t) => (
                    <li key={t.contractTaskId}>
                      <button
                        className="w-full px-4 py-2.5 text-left text-sm hover:bg-black/[0.04] dark:hover:bg-white/[0.04] transition-colors duration-[120ms]"
                        onClick={() => addTaskRow(t)}
                      >
                        <div className="font-medium text-[#3A4651] dark:text-white/90">{t.taskName}</div>
                        <div className="text-xs text-[#6B7682] dark:text-white/40">{t.contractSubject}</div>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
