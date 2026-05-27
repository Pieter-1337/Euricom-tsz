import { useCallback, useMemo, useState } from 'react';
import type { TimesheetWeek, SelectableContractTask, SelectableLeaveType } from '#/api/timesheets';
import { areMapsEqual } from '#/lib/utils';

export type TaskCellKey = `task:${string}:${string}`;
export type LeaveCellKey = `leave:${string}:${string}`;
export type CellKey = TaskCellKey | LeaveCellKey;

export function taskCellKey(contractTaskId: string, date: string): TaskCellKey {
  return `task:${contractTaskId}:${date}` as TaskCellKey;
}

export function leaveCellKey(leaveTypeId: string, date: string): LeaveCellKey {
  return `leave:${leaveTypeId}:${date}` as LeaveCellKey;
}

export interface BookingRow {
  id: string;
  name: string;
}

export interface UseWeekBookingsResult {
  taskRows: BookingRow[];
  leaveRows: BookingRow[];
  taskBookings: Map<TaskCellKey, number>;
  leaveBookings: Map<LeaveCellKey, number>;
  isDirty: boolean;
  setTaskCell: (taskId: string, date: string, value: number | null) => void;
  setLeaveCell: (leaveTypeId: string, date: string, value: number | null) => void;
  addTaskRow: (task: SelectableContractTask) => void;
  addLeaveRow: (lt: SelectableLeaveType) => void;
  removeTaskRow: (taskId: string) => void;
  removeLeaveRow: (leaveTypeId: string) => void;
  markSaved: () => void;
}

export function useWeekBookings(initialData: TimesheetWeek | null, isDraft: boolean): UseWeekBookingsResult {
  const initialTaskBookings = useMemo(() => {
    const map = new Map<TaskCellKey, number>();
    initialData?.timeEntries?.forEach((e) => {
      map.set(taskCellKey(e.contractTaskId, e.date), e.durationHours);
    });
    return map;
  }, [initialData]);

  const initialLeaveBookings = useMemo(() => {
    const map = new Map<LeaveCellKey, number>();
    initialData?.leaveBookings?.forEach((e) => {
      map.set(leaveCellKey(e.leaveTypeId, e.date), e.durationHours);
    });
    return map;
  }, [initialData]);

  const [taskBookings, setTaskBookings] = useState<Map<TaskCellKey, number>>(() => new Map(initialTaskBookings));
  const [leaveBookings, setLeaveBookings] = useState<Map<LeaveCellKey, number>>(() => new Map(initialLeaveBookings));
  const [savedTaskBookings, setSavedTaskBookings] = useState<Map<TaskCellKey, number>>(
    () => new Map(initialTaskBookings),
  );
  const [savedLeaveBookings, setSavedLeaveBookings] = useState<Map<LeaveCellKey, number>>(
    () => new Map(initialLeaveBookings),
  );

  const [taskRows, setTaskRows] = useState<BookingRow[]>(() => {
    const seen = new Map<string, string>();
    initialData?.timeEntries?.forEach((e) => {
      if (!seen.has(e.contractTaskId)) seen.set(e.contractTaskId, e.taskName);
    });
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }));
  });

  const [leaveRows, setLeaveRows] = useState<BookingRow[]>(() => {
    const seen = new Map<string, string>();
    initialData?.leaveBookings?.forEach((e) => {
      if (!seen.has(e.leaveTypeId)) seen.set(e.leaveTypeId, e.leaveTypeName);
    });
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }));
  });

  const isDirty = !areMapsEqual(taskBookings, savedTaskBookings) || !areMapsEqual(leaveBookings, savedLeaveBookings);

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
    },
    [isDraft],
  );

  const addTaskRow = useCallback((task: SelectableContractTask) => {
    setTaskRows((prev) =>
      prev.some((r) => r.id === task.contractTaskId)
        ? prev
        : [...prev, { id: task.contractTaskId, name: task.taskName }],
    );
  }, []);

  const addLeaveRow = useCallback((lt: SelectableLeaveType) => {
    setLeaveRows((prev) => (prev.some((r) => r.id === lt.id) ? prev : [...prev, { id: lt.id, name: lt.name }]));
  }, []);

  const removeTaskRow = useCallback((taskId: string) => {
    setTaskRows((prev) => prev.filter((r) => r.id !== taskId));
    setTaskBookings((prev) => {
      const next = new Map(prev);
      for (const key of next.keys()) {
        if (key.startsWith(`task:${taskId}:`)) next.delete(key);
      }
      return next;
    });
  }, []);

  const removeLeaveRow = useCallback((leaveTypeId: string) => {
    setLeaveRows((prev) => prev.filter((r) => r.id !== leaveTypeId));
    setLeaveBookings((prev) => {
      const next = new Map(prev);
      for (const key of next.keys()) {
        if (key.startsWith(`leave:${leaveTypeId}:`)) next.delete(key);
      }
      return next;
    });
  }, []);

  const markSaved = useCallback(() => {
    setSavedTaskBookings(new Map(taskBookings));
    setSavedLeaveBookings(new Map(leaveBookings));
  }, [taskBookings, leaveBookings]);

  return {
    taskRows,
    leaveRows,
    taskBookings,
    leaveBookings,
    isDirty,
    setTaskCell,
    setLeaveCell,
    addTaskRow,
    addLeaveRow,
    removeTaskRow,
    removeLeaveRow,
    markSaved,
  };
}
