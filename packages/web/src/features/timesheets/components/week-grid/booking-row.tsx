import { Trash2 } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { TableCell, TableRow } from '#/components/ui/table';
import { cn, formatHours } from '#/lib/utils';
import { getLeaveColor } from '#/features/leaves/use-leave-colors';
import { displayDuration, parseDurationInput } from '#/features/timesheets/iso-week';
import type { TimesheetWeek } from '#/api/timesheets';
import type { BookingRow as BookingRowData, CellKey } from './use-week-bookings';
import { taskCellKey, leaveCellKey } from './use-week-bookings';

type DayInfo = TimesheetWeek['days'][number];

interface BookingRowProps {
  variant: 'task' | 'leave';
  row: BookingRowData;
  days: DayInfo[];
  isDraft: boolean;
  getValue: (date: string) => number | undefined;
  onCellChange: (date: string, value: number | null) => void;
  cellInputs: Map<CellKey, string>;
  setCellInputs: React.Dispatch<React.SetStateAction<Map<CellKey, string>>>;
  onRemove: () => void;
}

export function BookingRow({
  variant,
  row,
  days,
  isDraft,
  getValue,
  onCellChange,
  cellInputs,
  setCellInputs,
  onRemove,
}: BookingRowProps) {
  const isLeave = variant === 'leave';
  const keyFor = (date: string): CellKey => (isLeave ? leaveCellKey(row.id, date) : taskCellKey(row.id, date));

  const rowTotal = days.reduce((sum, d) => sum + displayDuration(cellInputs.get(keyFor(d.date)), getValue(d.date)), 0);

  const handleRawChange = (date: string, raw: string) => {
    const key = keyFor(date);
    setCellInputs((prev) => new Map(prev).set(key, raw));
    if (raw === '' || raw === '0') {
      onCellChange(date, null);
      return;
    }
    const val = parseDurationInput(raw);
    if (val !== null) onCellChange(date, val);
  };

  const handleKeyDown = (date: string, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isDraft) return;
    const key = keyFor(date);
    if (e.key === 'd') {
      e.preventDefault();
      onCellChange(date, 8);
      setCellInputs((prev) => new Map(prev).set(key, '8'));
    } else if (e.key === 'h') {
      e.preventDefault();
      onCellChange(date, 4);
      setCellInputs((prev) => new Map(prev).set(key, '4'));
    } else if (e.key === 'Delete') {
      e.preventDefault();
      onCellChange(date, null);
      setCellInputs((prev) => new Map(prev).set(key, ''));
    }
  };

  return (
    <TableRow className="hover:bg-black/[0.01] dark:hover:bg-white/[0.01]">
      <TableCell
        className="px-4 py-2 font-medium text-[13px] max-w-[192px] text-[#3A4651] dark:text-white/80"
        title={row.name}
      >
        <div className="flex items-center gap-2">
          <span
            className={cn(
              'inline-block size-2.5 shrink-0 rounded-sm',
              isLeave ? getLeaveColor(row.id).bg : 'bg-green-600',
            )}
            aria-hidden="true"
          />
          <span className="truncate">{row.name}</span>
        </div>
      </TableCell>
      {days.map((d) => {
        const key = keyFor(d.date);
        const stored = getValue(d.date);
        const rawInput = cellInputs.get(key) ?? (stored !== undefined ? String(stored) : '');
        const isReadOnly = !isDraft || !d.isBusinessDay;
        const isInvalidInput = rawInput !== '' && rawInput !== '0' && parseDurationInput(rawInput) === null;

        return (
          <TableCell
            key={d.date}
            className={cn('px-1 py-1 text-center', !d.isBusinessDay && 'bg-black/[0.02] dark:bg-white/[0.01]')}
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
              <Input
                type="text"
                className={cn(
                  'h-8 w-16 px-1 py-0 text-center text-sm shadow-none',
                  'border-black/[0.10] bg-white dark:border-white/[0.10] dark:bg-transparent text-[#3A4651] dark:text-white/90',
                  'focus-visible:ring-0 focus-visible:border-input focus-visible:outline-[#00FF00] focus-visible:outline-2 focus-visible:outline-offset-1',
                  stored !== undefined && 'font-semibold',
                  isInvalidInput &&
                    'border-red-500 bg-red-50 text-red-700 dark:border-red-500 dark:bg-red-950/30 dark:text-red-400 focus-visible:outline-red-500',
                )}
                value={rawInput}
                placeholder=""
                onChange={(e) => handleRawChange(d.date, e.target.value)}
                onKeyDown={(e) => handleKeyDown(d.date, e)}
              />
            )}
          </TableCell>
        );
      })}
      <TableCell className="px-2 py-2 text-center font-semibold text-sm text-[#3A4651] dark:text-white/80">
        {rowTotal > 0 ? formatHours(rowTotal) : ''}
      </TableCell>
      {isDraft && (
        <TableCell className="px-1 py-2 text-center">
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 text-[#6B7682] hover:text-red-600 dark:text-white/40 dark:hover:text-red-400"
            onClick={onRemove}
            title="Remove row"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </TableCell>
      )}
    </TableRow>
  );
}
