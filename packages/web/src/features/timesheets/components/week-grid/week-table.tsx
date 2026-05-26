import { cn } from '#/lib/utils';
import { formatDayHeader } from '#/features/timesheets/iso-week';
import type { TimesheetWeek } from '#/api/timesheets';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { BookingRow } from './booking-row';
import { taskCellKey, leaveCellKey } from './use-week-bookings';
import type { BookingRow as BookingRowData, CellKey, LeaveCellKey, TaskCellKey } from './use-week-bookings';
import { WORKDAY_CAPACITY } from './use-week-flush';

type DayInfo = TimesheetWeek['days'][number];
type Status = TimesheetWeek['status'];

interface WeekTableProps {
  days: DayInfo[];
  taskRows: BookingRowData[];
  leaveRows: BookingRowData[];
  taskBookings: Map<TaskCellKey, number>;
  leaveBookings: Map<LeaveCellKey, number>;
  dayTotals: Map<string, number>;
  weekTotal: number;
  isDraft: boolean;
  status: Status;
  cellInputs: Map<CellKey, string>;
  setCellInputs: React.Dispatch<React.SetStateAction<Map<CellKey, string>>>;
  editingCell: CellKey | null;
  setEditingCell: React.Dispatch<React.SetStateAction<CellKey | null>>;
  setTaskCell: (taskId: string, date: string, value: number | null) => void;
  setLeaveCell: (leaveTypeId: string, date: string, value: number | null) => void;
  onRemoveTaskRow: (taskId: string) => void;
  onRemoveLeaveRow: (leaveTypeId: string) => void;
}

export function WeekTable({
  days,
  taskRows,
  leaveRows,
  taskBookings,
  leaveBookings,
  dayTotals,
  weekTotal,
  isDraft,
  status,
  cellInputs,
  setCellInputs,
  editingCell,
  setEditingCell,
  setTaskCell,
  setLeaveCell,
  onRemoveTaskRow,
  onRemoveLeaveRow,
}: WeekTableProps) {
  const hasRows = taskRows.length > 0 || leaveRows.length > 0;
  const emptyColSpan = isDraft ? 10 : 9;

  return (
    <div
      className={cn(
        'rounded-lg border border-black/[0.08] dark:border-white/[0.06]',
        status === 'Submitted' && 'bg-green-50 dark:bg-green-950/20',
        status === 'Approved' && 'bg-green-100 dark:bg-green-900/20',
      )}
    >
      <Table>
        <TableHeader>
          <TableRow className="bg-[#F1F5F6] dark:bg-[#1D252D] hover:bg-[#F1F5F6] dark:hover:bg-[#1D252D]">
            <TableHead className="h-auto w-48 px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6B7682] dark:text-white/40">
              Task
            </TableHead>
            {days.map((d) => {
              const { day, label } = formatDayHeader(d.date);
              const isHoliday = Boolean(d.holidayName);
              return (
                <TableHead
                  key={d.date}
                  className={cn(
                    'h-auto min-w-[72px] px-2 py-3 text-center',
                    !d.isBusinessDay && !isHoliday && 'opacity-40',
                  )}
                  title={d.holidayName ?? undefined}
                >
                  <div
                    className={cn(
                      'text-[10px] font-medium uppercase tracking-[0.2em]',
                      isHoliday ? 'text-amber-600 dark:text-amber-400' : 'text-[#6B7682] dark:text-white/40',
                    )}
                  >
                    {day}
                  </div>
                  <div
                    className={cn(
                      'text-xs font-semibold',
                      isHoliday ? 'text-amber-600 dark:text-amber-400' : 'text-[#3A4651] dark:text-white/70',
                    )}
                  >
                    {label}
                  </div>
                </TableHead>
              );
            })}
            <TableHead className="h-auto min-w-[60px] px-2 py-3 text-center text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6B7682] dark:text-white/40">
              Total
            </TableHead>
            {isDraft && <TableHead className="w-10" />}
          </TableRow>
        </TableHeader>
        <TableBody>
          {!hasRows && (
            <TableRow className="hover:bg-transparent">
              <TableCell
                colSpan={emptyColSpan}
                className="px-4 py-8 text-center text-sm text-[#6B7682] dark:text-white/40"
              >
                No tasks added. Use the buttons below to add a task or leave row.
              </TableCell>
            </TableRow>
          )}

          {taskRows.map((row) => (
            <BookingRow
              key={`task-${row.id}`}
              variant="task"
              row={row}
              days={days}
              isDraft={isDraft}
              getValue={(date) => taskBookings.get(taskCellKey(row.id, date))}
              onCellChange={(date, value) => setTaskCell(row.id, date, value)}
              cellInputs={cellInputs}
              setCellInputs={setCellInputs}
              editingCell={editingCell}
              setEditingCell={setEditingCell}
              onRemove={() => onRemoveTaskRow(row.id)}
            />
          ))}

          {leaveRows.map((row) => (
            <BookingRow
              key={`leave-${row.id}`}
              variant="leave"
              row={row}
              days={days}
              isDraft={isDraft}
              getValue={(date) => leaveBookings.get(leaveCellKey(row.id, date))}
              onCellChange={(date, value) => setLeaveCell(row.id, date, value)}
              cellInputs={cellInputs}
              setCellInputs={setCellInputs}
              editingCell={editingCell}
              setEditingCell={setEditingCell}
              onRemove={() => onRemoveLeaveRow(row.id)}
            />
          ))}
        </TableBody>
        <TableFooter className="bg-[#F1F5F6] dark:bg-[#1D252D]">
          <TableRow className="hover:bg-transparent">
            <TableCell className="px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.2em] text-[#6B7682] dark:text-white/40">
              Day total
            </TableCell>
            {days.map((d) => {
              const total = dayTotals.get(d.date) ?? 0;
              const overCap = total > WORKDAY_CAPACITY;
              return (
                <TableCell
                  key={d.date}
                  className={cn(
                    'px-2 py-2 text-center font-semibold text-sm',
                    overCap ? 'text-red-600 dark:text-red-400' : 'text-[#3A4651] dark:text-white/80',
                  )}
                  data-testid={`day-total-${d.date}`}
                >
                  {overCap ? `${total}h / ${WORKDAY_CAPACITY}h max` : total > 0 ? total : ''}
                </TableCell>
              );
            })}
            <TableCell className="px-2 py-2 text-center font-bold text-sm text-[#3A4651] dark:text-white">
              {weekTotal > 0 ? weekTotal : ''}
            </TableCell>
            {isDraft && <TableCell />}
          </TableRow>
        </TableFooter>
      </Table>
    </div>
  );
}
