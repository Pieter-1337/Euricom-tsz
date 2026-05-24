import { useCallback, useEffect, useState } from 'react';
import { useBlocker, useNavigate, useRouter } from '@tanstack/react-router';
import { Calendar, ChevronLeft, ChevronRight, Plus, Trash2 } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Calendar as CalendarPicker } from '#/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { cn } from '#/lib/utils';
import type {
  TimesheetWeek,
  SelectableContractTask,
  SelectableLeaveType,
  TimeEntryInput,
  LeaveBookingInput,
} from '#/api/timesheets';
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
import { parseServerError } from '#/lib/server-error';

type TaskCellKey = `task:${string}:${string}`; // `task:${contractTaskId}:${date}`
type LeaveCellKey = `leave:${string}:${string}`; // `leave:${leaveTypeId}:${date}`
type CellKey = TaskCellKey | LeaveCellKey;

interface TimesheetWeekGridProps {
  userId: string;
  year: number;
  week: number;
  initialData: TimesheetWeek | null;
  selectableTasks: SelectableContractTask[];
  selectableLeaveTypes: SelectableLeaveType[];
  isAdmin: boolean;
}

function taskCellKey(contractTaskId: string, date: string): TaskCellKey {
  return `task:${contractTaskId}:${date}` as TaskCellKey;
}

function leaveCellKey(leaveTypeId: string, date: string): LeaveCellKey {
  return `leave:${leaveTypeId}:${date}` as LeaveCellKey;
}

export function TimesheetWeekGrid({
  userId,
  year,
  week,
  initialData,
  selectableTasks,
  selectableLeaveTypes,
  isAdmin,
}: TimesheetWeekGridProps) {
  const navigate = useNavigate();
  const router = useRouter();

  const [taskBookings, setTaskBookings] = useState<Map<TaskCellKey, number>>(() => {
    const map = new Map<TaskCellKey, number>();
    initialData?.timeEntries?.forEach((e) => {
      map.set(taskCellKey(e.contractTaskId, e.date), e.durationHours);
    });
    return map;
  });

  const [leaveBookings, setLeaveBookings] = useState<Map<LeaveCellKey, number>>(() => {
    const map = new Map<LeaveCellKey, number>();
    initialData?.leaveBookings?.forEach((e) => {
      map.set(leaveCellKey(e.leaveTypeId, e.date), e.durationHours);
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

  const [leaveRows, setLeaveRows] = useState<Array<{ id: string; name: string }>>(() => {
    const seen = new Map<string, string>();
    initialData?.leaveBookings?.forEach((e) => {
      if (!seen.has(e.leaveTypeId)) seen.set(e.leaveTypeId, e.leaveTypeName);
    });
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }));
  });

  const [isDirty, setIsDirty] = useState(false);
  const [isFlushing, setIsFlushing] = useState(false);
  const [isLifecycleLoading, setIsLifecycleLoading] = useState(false);
  const [showTaskPicker, setShowTaskPicker] = useState(false);
  const [showLeavePicker, setShowLeavePicker] = useState(false);
  const [showCalendar, setShowCalendar] = useState(false);
  const [editingCell, setEditingCell] = useState<CellKey | null>(null);
  const [cellInputs, setCellInputs] = useState<Map<CellKey, string>>(new Map());
  const [flushError, setFlushError] = useState<string | null>(null);

  const days = initialData?.days ?? [];
  const status = initialData?.status ?? 'Draft';
  const isDraft = status === 'Draft';

  const flush = useCallback(async (): Promise<boolean> => {
    if (!isDirty || isFlushing) return true;
    setIsFlushing(true);
    setFlushError(null);
    try {
      const timeEntries: TimeEntryInput[] = [];
      taskBookings.forEach((durationHours, key) => {
        const [, contractTaskId, date] = key.split(':');
        timeEntries.push({ contractTaskId, date, durationHours });
      });

      const leaveBookingInputs: LeaveBookingInput[] = [];
      leaveBookings.forEach((durationHours, key) => {
        const [, leaveTypeId, date] = key.split(':');
        leaveBookingInputs.push({ leaveTypeId, date, durationHours });
      });

      await submitTimesheetBookings({
        data: { userId, year, week, timeEntries, leaveBookings: leaveBookingInputs },
      });
      setIsDirty(false);
      return true;
    } catch (e) {
      const apiErr = parseServerError(e);
      if (apiErr?.problem?.code === 'ERR_TIMESHEET_LEAVE_ALLOWANCE_EXCEEDED') {
        setFlushError(apiErr.problem.detail ?? 'Leave allowance exceeded.');
      } else {
        setFlushError(apiErr?.problem?.detail ?? 'Could not save changes — please try again');
      }
      return false;
    } finally {
      setIsFlushing(false);
    }
  }, [isDirty, isFlushing, taskBookings, leaveBookings, userId, year, week]);

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

  const setTaskCell = useCallback(
    (taskId: string, date: string, value: number | null) => {
      if (!isDraft) return;
      setTaskBookings((prev) => {
        const next = new Map(prev);
        const key = taskCellKey(taskId, date);
        if (value === null) next.delete(key);
        else next.set(key, value);
        return next;
      });
      setIsDirty(true);
    },
    [isDraft],
  );

  const setLeaveCell = useCallback(
    (leaveTypeId: string, date: string, value: number | null) => {
      if (!isDraft) return;
      setLeaveBookings((prev) => {
        const next = new Map(prev);
        const key = leaveCellKey(leaveTypeId, date);
        if (value === null) next.delete(key);
        else next.set(key, value);
        return next;
      });
      setIsDirty(true);
    },
    [isDraft],
  );

  const handleTaskCellKeyDown = (taskId: string, date: string, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isDraft) return;
    const key = taskCellKey(taskId, date);
    if (e.key === 'd') {
      e.preventDefault();
      setTaskCell(taskId, date, 8);
      setCellInputs((prev) => new Map(prev).set(key, '8'));
    } else if (e.key === 'h') {
      e.preventDefault();
      setTaskCell(taskId, date, 4);
      setCellInputs((prev) => new Map(prev).set(key, '4'));
    } else if (e.key === 'Delete') {
      e.preventDefault();
      setTaskCell(taskId, date, null);
      setCellInputs((prev) => new Map(prev).set(key, ''));
    }
  };

  const handleLeaveCellKeyDown = (leaveTypeId: string, date: string, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isDraft) return;
    const key = leaveCellKey(leaveTypeId, date);
    if (e.key === 'd') {
      e.preventDefault();
      setLeaveCell(leaveTypeId, date, 8);
      setCellInputs((prev) => new Map(prev).set(key, '8'));
    } else if (e.key === 'h') {
      e.preventDefault();
      setLeaveCell(leaveTypeId, date, 4);
      setCellInputs((prev) => new Map(prev).set(key, '4'));
    } else if (e.key === 'Delete') {
      e.preventDefault();
      setLeaveCell(leaveTypeId, date, null);
      setCellInputs((prev) => new Map(prev).set(key, ''));
    }
  };

  const handleTaskCellChange = (taskId: string, date: string, raw: string) => {
    const key = taskCellKey(taskId, date);
    setCellInputs((prev) => new Map(prev).set(key, raw));
    if (raw === '' || raw === '0') {
      setTaskCell(taskId, date, null);
      return;
    }
    const val = parseDurationInput(raw);
    if (val !== null) setTaskCell(taskId, date, val);
  };

  const handleLeaveCellChange = (leaveTypeId: string, date: string, raw: string) => {
    const key = leaveCellKey(leaveTypeId, date);
    setCellInputs((prev) => new Map(prev).set(key, raw));
    if (raw === '' || raw === '0') {
      setLeaveCell(leaveTypeId, date, null);
      return;
    }
    const val = parseDurationInput(raw);
    if (val !== null) setLeaveCell(leaveTypeId, date, val);
  };

  const handleCellBlur = (key: CellKey, storedValue: number | undefined) => {
    setEditingCell(null);
    const raw = cellInputs.get(key) ?? '';
    if (raw === '') return;
    const val = parseDurationInput(raw);
    if (val === null) {
      setCellInputs((prev) => {
        const next = new Map(prev);
        next.set(key, storedValue !== undefined ? String(storedValue) : '');
        return next;
      });
    }
  };

  const addTaskRow = (task: SelectableContractTask) => {
    if (!taskRows.some((r) => r.id === task.contractTaskId)) {
      setTaskRows((prev) => [...prev, { id: task.contractTaskId, name: task.taskName }]);
    }
    setShowTaskPicker(false);
  };

  const addLeaveRow = (lt: SelectableLeaveType) => {
    if (!leaveRows.some((r) => r.id === lt.id)) {
      setLeaveRows((prev) => [...prev, { id: lt.id, name: lt.name }]);
    }
    setShowLeavePicker(false);
  };

  const removeTaskRow = (taskId: string) => {
    setTaskRows((prev) => prev.filter((r) => r.id !== taskId));
    setTaskBookings((prev) => {
      const next = new Map(prev);
      for (const key of next.keys()) {
        if (key.startsWith(`task:${taskId}:`)) next.delete(key);
      }
      return next;
    });
    setIsDirty(true);
  };

  const removeLeaveRow = (leaveTypeId: string) => {
    setLeaveRows((prev) => prev.filter((r) => r.id !== leaveTypeId));
    setLeaveBookings((prev) => {
      const next = new Map(prev);
      for (const key of next.keys()) {
        if (key.startsWith(`leave:${leaveTypeId}:`)) next.delete(key);
      }
      return next;
    });
    setIsDirty(true);
  };

  const handleSubmit = async () => {
    setIsLifecycleLoading(true);
    try {
      const flushed = await flush();
      if (!flushed) {
        setFlushError((prev) => prev ?? 'Could not save changes — please try again');
        return;
      }
      await submitWeekLifecycle({ data: { userId, year, week } });
      await router.invalidate();
    } catch {
      // error stays silent
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
      // error stays silent
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
      // error stays silent
    } finally {
      setIsLifecycleLoading(false);
    }
  };

  const { year: prevYear, week: prevWeekNum } = prevWeek(year, week);
  const { year: nextYear, week: nextWeekNum } = nextWeek(year, week);
  const today = todayWeek();

  const navigateToWeek = async (y: number, w: number) => {
    const saved = await flush();
    if (!saved) return;
    await navigate({ to: '/timesheets/week/$year/$week', params: { year: String(y), week: String(w) } });
  };

  // Day totals: sum task bookings + leave bookings
  const dayTotals = new Map<string, number>();
  days.forEach((d) => {
    let total = 0;
    taskRows.forEach((r) => {
      total += taskBookings.get(taskCellKey(r.id, d.date)) ?? 0;
    });
    leaveRows.forEach((r) => {
      total += leaveBookings.get(leaveCellKey(r.id, d.date)) ?? 0;
    });
    dayTotals.set(d.date, total);
  });

  const weekTotal = Array.from(dayTotals.values()).reduce((a, b) => a + b, 0);

  const addableTaskIds = new Set(taskRows.map((r) => r.id));
  const availableTasksToAdd = selectableTasks.filter((t) => !addableTaskIds.has(t.contractTaskId));

  const addableLeaveIds = new Set(leaveRows.map((r) => r.id));
  const availableLeaveToAdd = selectableLeaveTypes.filter((lt) => !addableLeaveIds.has(lt.id));

  const hasRows = taskRows.length > 0 || leaveRows.length > 0;

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
          {flushError && <span className="text-xs text-red-500">Could not save changes — please try again</span>}
          {isDraft && (
            <Button
              variant="outline"
              size="sm"
              disabled={!isDirty || isFlushing || isLifecycleLoading}
              onClick={() => void flush()}
            >
              Save
            </Button>
          )}
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

      {/* Allowance error banner */}
      {flushError && (
        <div className="rounded-md border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 dark:border-red-800 dark:bg-red-950/30 dark:text-red-400">
          {flushError}
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
              {isDraft && <th className="w-10" />}
            </tr>
          </thead>
          <tbody>
            {!hasRows && (
              <tr>
                <td colSpan={isDraft ? 10 : 9} className="px-4 py-8 text-center text-sm text-[#6B7682] dark:text-white/40">
                  No tasks added. Use the buttons below to add a task or leave row.
                </td>
              </tr>
            )}

            {/* Task rows */}
            {taskRows.map((row) => {
              const rowTotal = days.reduce((sum, d) => sum + (taskBookings.get(taskCellKey(row.id, d.date)) ?? 0), 0);
              return (
                <tr
                  key={`task-${row.id}`}
                  className="border-t border-black/[0.04] dark:border-white/[0.04] hover:bg-black/[0.01] dark:hover:bg-white/[0.01]"
                >
                  <td
                    className="px-4 py-2 font-medium text-[13px] text-[#3A4651] dark:text-white/80 truncate max-w-[192px]"
                    title={row.name}
                  >
                    {row.name}
                  </td>
                  {days.map((d) => {
                    const key = taskCellKey(row.id, d.date);
                    const stored = taskBookings.get(key);
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
                            onChange={(e) => handleTaskCellChange(row.id, d.date, e.target.value)}
                            onBlur={() => handleCellBlur(key, stored)}
                            onKeyDown={(e) => handleTaskCellKeyDown(row.id, d.date, e)}
                          />
                        )}
                      </td>
                    );
                  })}
                  <td className="px-2 py-2 text-center font-semibold text-sm text-[#3A4651] dark:text-white/80">
                    {rowTotal > 0 ? rowTotal : ''}
                  </td>
                  {isDraft && (
                    <td className="px-1 py-2 text-center">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7 text-[#6B7682] hover:text-red-600 dark:text-white/40 dark:hover:text-red-400"
                        onClick={() => removeTaskRow(row.id)}
                        title="Remove row"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  )}
                </tr>
              );
            })}

            {/* Leave rows */}
            {leaveRows.map((row) => {
              const rowTotal = days.reduce((sum, d) => sum + (leaveBookings.get(leaveCellKey(row.id, d.date)) ?? 0), 0);
              return (
                <tr
                  key={`leave-${row.id}`}
                  className="border-t border-black/[0.04] dark:border-white/[0.04] hover:bg-amber-50/50 dark:hover:bg-amber-900/10"
                >
                  <td
                    className="px-4 py-2 font-medium text-[13px] text-amber-700 dark:text-amber-400 truncate max-w-[192px]"
                    title={row.name}
                  >
                    {row.name}
                  </td>
                  {days.map((d) => {
                    const key = leaveCellKey(row.id, d.date);
                    const stored = leaveBookings.get(key);
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
                              stored !== undefined && 'font-semibold text-amber-700 dark:text-amber-400',
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
                              'border-amber-300/60 bg-amber-50 dark:border-amber-700/40 dark:bg-amber-950/20',
                              'text-amber-800 dark:text-amber-300 placeholder:text-[#6B7682]/50',
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
                            onChange={(e) => handleLeaveCellChange(row.id, d.date, e.target.value)}
                            onBlur={() => handleCellBlur(key, stored)}
                            onKeyDown={(e) => handleLeaveCellKeyDown(row.id, d.date, e)}
                          />
                        )}
                      </td>
                    );
                  })}
                  <td className="px-2 py-2 text-center font-semibold text-sm text-amber-700 dark:text-amber-400">
                    {rowTotal > 0 ? rowTotal : ''}
                  </td>
                  {isDraft && (
                    <td className="px-1 py-2 text-center">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7 text-[#6B7682] hover:text-red-600 dark:text-white/40 dark:hover:text-red-400"
                        onClick={() => removeLeaveRow(row.id)}
                        title="Remove row"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  )}
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
              {isDraft && <td />}
            </tr>
          </tfoot>
        </table>
      </div>

      {/* Add task row / leave row buttons */}
      {isDraft && (
        <div className="flex items-center gap-2">
          <Popover open={showTaskPicker} onOpenChange={setShowTaskPicker}>
            <PopoverTrigger asChild>
              <Button variant="ghost" size="sm" className="gap-2 text-[13px]">
                <Plus className="h-4 w-4" />
                Add task row
              </Button>
            </PopoverTrigger>
            <PopoverContent align="start" className="w-72 p-0">
              {availableTasksToAdd.length === 0 ? (
                <div className="px-4 py-3 text-sm text-[#6B7682] dark:text-white/40">
                  No more tasks available for this week.
                </div>
              ) : (
                <ul>
                  {availableTasksToAdd.map((t) => (
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
            </PopoverContent>
          </Popover>

          <Popover open={showLeavePicker} onOpenChange={setShowLeavePicker}>
            <PopoverTrigger asChild>
              <Button
                variant="ghost"
                size="sm"
                className="gap-2 text-[13px] text-amber-700 hover:text-amber-800 dark:text-amber-400"
              >
                <Plus className="h-4 w-4" />
                Add leave row
              </Button>
            </PopoverTrigger>
            <PopoverContent align="start" className="w-64 p-0">
              {availableLeaveToAdd.length === 0 ? (
                <div className="px-4 py-3 text-sm text-[#6B7682] dark:text-white/40">
                  No more leave types available.
                </div>
              ) : (
                <ul>
                  {availableLeaveToAdd.map((lt) => (
                    <li key={lt.id}>
                      <button
                        className="w-full px-4 py-2.5 text-left text-sm hover:bg-black/[0.04] dark:hover:bg-white/[0.04] transition-colors duration-[120ms]"
                        onClick={() => addLeaveRow(lt)}
                      >
                        <div className="font-medium text-amber-700 dark:text-amber-400">{lt.name}</div>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </PopoverContent>
          </Popover>
        </div>
      )}
    </div>
  );
}
