import { useState } from 'react';
import { useNavigate, useRouter } from '@tanstack/react-router';
import { cn } from '#/lib/utils';
import { getLeaveColor } from '#/features/leaves/use-leave-colors';
import type { TimesheetWeek, SelectableContractTask, SelectableLeaveType } from '#/api/timesheets';
import { taskCellKey, leaveCellKey, useWeekBookings } from './use-week-bookings';
import type { CellKey } from './use-week-bookings';
import {
  CELL_INVALID_ERROR_MESSAGE,
  DAY_CAPACITY_ERROR_MESSAGE,
  WORKDAY_CAPACITY,
  useWeekFlush,
} from './use-week-flush';
import { displayDuration, validateDurationInput } from '#/features/timesheets/iso-week';
import { approveWeekLifecycle, reopenWeekLifecycle, submitWeekLifecycle } from '#/features/timesheets/server-fns';
import { WeekActionsBar } from './week-actions-bar';
import { WeekTable } from './week-table';
import { AddRowPopover } from './add-row-popover';

interface TimesheetWeekGridProps {
  userId: string;
  year: number;
  week: number;
  initialData: TimesheetWeek | null;
  selectableTasks: SelectableContractTask[];
  selectableLeaveTypes: SelectableLeaveType[];
  isAdmin: boolean;
  isReadOnly?: boolean;
}

export function TimesheetWeekGrid({
  userId,
  year,
  week,
  initialData,
  selectableTasks,
  selectableLeaveTypes,
  isAdmin,
  isReadOnly = false,
}: TimesheetWeekGridProps) {
  const navigate = useNavigate();
  const router = useRouter();

  const status = initialData?.status ?? 'Draft';
  const isDraft = status === 'Draft';
  // canEdit: user can modify bookings only when not in read-only mode and week is Draft
  const canEdit = !isReadOnly && isDraft;
  const days = initialData?.days ?? [];

  const bookings = useWeekBookings(initialData, canEdit);
  const { flush, isFlushing, flushError } = useWeekFlush({
    userId,
    year,
    week,
    taskBookings: bookings.taskBookings,
    leaveBookings: bookings.leaveBookings,
    isDirty: bookings.isDirty,
    markSaved: bookings.markSaved,
  });

  const [isLifecycleLoading, setIsLifecycleLoading] = useState(false);
  const [cellInputs, setCellInputs] = useState<Map<CellKey, string>>(new Map());

  // Day totals: sum what the user sees in each cell (raw input when present,
  // falling back to committed stored value). Invalid-but-numeric inputs like
  // "44" or "0.15" contribute to the total even though they can't be saved.
  const dayTotals = new Map<string, number>();
  days.forEach((d) => {
    let total = 0;
    bookings.taskRows.forEach((r) => {
      const key = taskCellKey(r.id, d.date);
      total += displayDuration(cellInputs.get(key), bookings.taskBookings.get(key));
    });
    bookings.leaveRows.forEach((r) => {
      const key = leaveCellKey(r.id, d.date);
      total += displayDuration(cellInputs.get(key), bookings.leaveBookings.get(key));
    });
    dayTotals.set(d.date, total);
  });

  const weekTotal = Array.from(dayTotals.values()).reduce((a, b) => a + b, 0);
  const hasDayCapacityError = Array.from(dayTotals.values()).some((t) => t > WORKDAY_CAPACITY);

  const cellValidations = Array.from(cellInputs.values())
    .filter((raw) => raw !== '' && raw !== '0')
    .map(validateDurationInput);
  const hasInvalidCellInput = cellValidations.includes('invalid');
  const hasExceedsCellInput = cellValidations.includes('exceeds');

  const showExceedsError = hasDayCapacityError || hasExceedsCellInput;
  const showInvalidError = hasInvalidCellInput;

  const errorMessages: string[] = [];
  if (showInvalidError) errorMessages.push(CELL_INVALID_ERROR_MESSAGE);
  if (showExceedsError) errorMessages.push(DAY_CAPACITY_ERROR_MESSAGE);
  if (errorMessages.length === 0 && flushError) errorMessages.push(flushError);

  const addableTaskIds = new Set(bookings.taskRows.map((r) => r.id));
  const availableTasksToAdd = selectableTasks.filter((t) => !addableTaskIds.has(t.contractTaskId));

  const addableLeaveIds = new Set(bookings.leaveRows.map((r) => r.id));
  const availableLeaveToAdd = selectableLeaveTypes.filter((lt) => !addableLeaveIds.has(lt.id));

  const navigateToWeek = async (y: number, w: number) => {
    if (!isReadOnly) {
      const saved = await flush();
      if (!saved) return;
    }
    await navigate({
      to: '/time-entry/week/$year/$week',
      params: { year: String(y), week: String(w) },
      search: (prev) => prev,
    });
  };

  const handleSubmit = async () => {
    setIsLifecycleLoading(true);
    try {
      const flushed = await flush();
      if (!flushed) return;
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

  return (
    <div className="flex flex-col gap-4">
      <WeekActionsBar
        year={year}
        week={week}
        status={status}
        isDraft={isDraft}
        isAdmin={isAdmin}
        isReadOnly={isReadOnly}
        isDirty={bookings.isDirty}
        isFlushing={isFlushing}
        isLifecycleLoading={isLifecycleLoading}
        hasDayCapacityError={showExceedsError}
        hasInvalidCellInput={showInvalidError}
        onNavigateToWeek={(y, w) => void navigateToWeek(y, w)}
        onSave={() => void flush()}
        onSubmit={() => void handleSubmit()}
        onApprove={() => void handleApprove()}
        onReopen={() => void handleReopen()}
        navExtras={
          canEdit ? (
            <>
              <AddRowPopover
                triggerLabel="Add task"
                items={availableTasksToAdd}
                getKey={(t) => t.contractTaskId}
                renderItem={(t) => (
                  <div className="flex items-center gap-2">
                    <span className="inline-block size-2.5 shrink-0 rounded-sm bg-green-600" aria-hidden="true" />
                    <div className="font-medium text-[#3A4651] dark:text-white/90">
                      {t.taskName}{' '}
                      <span className="text-xs text-[#6B7682] dark:text-white/40">({t.contractSubject})</span>
                    </div>
                  </div>
                )}
                onAdd={bookings.addTaskRow}
                emptyMessage="No more tasks available for this week."
                contentWidth="w-72"
              />
              <AddRowPopover
                triggerLabel="Add leave"
                items={availableLeaveToAdd}
                getKey={(lt) => lt.id}
                renderItem={(lt) => (
                  <div className="flex items-center gap-2">
                    <span
                      className={cn('inline-block size-2.5 shrink-0 rounded-sm', getLeaveColor(lt.id).bg)}
                      aria-hidden="true"
                    />
                    <div className="font-medium text-[#3A4651] dark:text-white/90">{lt.name}</div>
                  </div>
                )}
                onAdd={bookings.addLeaveRow}
                emptyMessage="No more leave types available."
                contentWidth="w-64"
              />
            </>
          ) : undefined
        }
      />

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

      {errorMessages.length > 0 && (
        <div className="flex flex-col gap-1 rounded-md border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 dark:border-red-800 dark:bg-red-950/30 dark:text-red-400">
          {errorMessages.map((m) => (
            <div key={m}>{m}</div>
          ))}
        </div>
      )}

      <WeekTable
        days={days}
        taskRows={bookings.taskRows}
        leaveRows={bookings.leaveRows}
        taskBookings={bookings.taskBookings}
        leaveBookings={bookings.leaveBookings}
        dayTotals={dayTotals}
        weekTotal={weekTotal}
        canEdit={canEdit}
        status={status}
        cellInputs={cellInputs}
        setCellInputs={setCellInputs}
        setTaskCell={bookings.setTaskCell}
        setLeaveCell={bookings.setLeaveCell}
        onRemoveTaskRow={bookings.removeTaskRow}
        onRemoveLeaveRow={bookings.removeLeaveRow}
      />
    </div>
  );
}
