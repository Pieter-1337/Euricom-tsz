import { useCallback, useEffect, useRef, useState } from 'react';
import { useBlocker } from '@tanstack/react-router';
import type { TimeEntryInput, LeaveBookingInput } from '#/api/timesheets';
import { submitTimesheetBookings } from '#/features/timesheets/server-fns';
import { parseServerError } from '#/lib/server-error';
import type { LeaveCellKey, TaskCellKey } from './use-week-bookings';

/** Mirrors backend `TimesheetWeek.WorkdayCapacity`. */
export const WORKDAY_CAPACITY = 8;
export const DAY_CAPACITY_ERROR_MESSAGE = 'One or more days exceed the daily capacity.';
export const CELL_INVALID_ERROR_MESSAGE =
  'One or more cells contain an invalid value (must be a multiple of 0.25 between 0.25 and 8).';

interface UseWeekFlushParams {
  userId: string;
  year: number;
  week: number;
  taskBookings: Map<TaskCellKey, number>;
  leaveBookings: Map<LeaveCellKey, number>;
  isDirty: boolean;
  markSaved: () => void;
}

export interface UseWeekFlushResult {
  flush: () => Promise<boolean>;
  isFlushing: boolean;
  flushError: string | null;
}

export function useWeekFlush({
  userId,
  year,
  week,
  taskBookings,
  leaveBookings,
  isDirty,
  markSaved,
}: UseWeekFlushParams): UseWeekFlushResult {
  const [isFlushing, setIsFlushing] = useState(false);
  const [flushError, setFlushError] = useState<string | null>(null);
  // Share a single in-flight save across concurrent callers (Save button, navigation, useBlocker)
  // so they all await the same network round-trip instead of skipping past it.
  const inFlightRef = useRef<Promise<boolean> | null>(null);

  const flush = useCallback(async (): Promise<boolean> => {
    if (inFlightRef.current) return inFlightRef.current;
    if (!isDirty) return true;

    const run = async (): Promise<boolean> => {
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

        // Local pre-check: mirror the backend day-capacity invariant. Short-circuit
        // without a network call so the whole week's save is blocked, not partially applied.
        const perDay = new Map<string, number>();
        for (const e of timeEntries) perDay.set(e.date, (perDay.get(e.date) ?? 0) + e.durationHours);
        for (const b of leaveBookingInputs) perDay.set(b.date, (perDay.get(b.date) ?? 0) + b.durationHours);
        for (const total of perDay.values()) {
          if (total > WORKDAY_CAPACITY) {
            setFlushError(DAY_CAPACITY_ERROR_MESSAGE);
            return false;
          }
        }

        await submitTimesheetBookings({
          data: { userId, year, week, timeEntries, leaveBookings: leaveBookingInputs },
        });
        markSaved();
        return true;
      } catch (e) {
        const apiErr = parseServerError(e);
        if (apiErr?.problem?.code === 'ERR_TIMESHEET_LEAVE_ALLOWANCE_EXCEEDED') {
          setFlushError(apiErr.problem.detail ?? 'Leave allowance exceeded.');
        } else if (apiErr?.problem?.code === 'ERR_TIMESHEET_DAY_CAPACITY_EXCEEDED') {
          setFlushError(apiErr.problem.detail ?? DAY_CAPACITY_ERROR_MESSAGE);
        } else {
          setFlushError(apiErr?.problem?.detail ?? 'Could not save changes — please try again');
        }
        return false;
      } finally {
        setIsFlushing(false);
      }
    };

    const promise = run();
    inFlightRef.current = promise;
    try {
      return await promise;
    } finally {
      inFlightRef.current = null;
    }
  }, [isDirty, taskBookings, leaveBookings, userId, year, week, markSaved]);

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

  return { flush, isFlushing, flushError };
}
