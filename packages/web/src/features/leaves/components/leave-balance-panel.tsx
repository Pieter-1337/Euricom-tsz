import { cn } from '#/lib/utils';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { getLeaveColor } from '#/features/leaves/use-leave-colors';
import type { LeaveSummaryResult, DisplayValue } from '#/features/leaves/compute-leave-summary';

function displayVal(val: DisplayValue): string {
  if (val === '—') return '—';
  // Show at most 1 decimal place, trim trailing zeros
  const rounded = Math.round(val * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

interface ColorDotProps {
  leaveTypeId: string | null;
}

function ColorDot({ leaveTypeId }: ColorDotProps) {
  if (leaveTypeId === '__feestdagen__') {
    return <span className="inline-block h-2.5 w-2.5 rounded-sm bg-amber-600/90" aria-hidden="true" />;
  }
  if (!leaveTypeId) {
    return (
      <span
        className="inline-block h-2.5 w-2.5 rounded-sm bg-[#3A4651]/40 dark:bg-white/25"
        aria-hidden="true"
      />
    );
  }
  const color = getLeaveColor(leaveTypeId);
  return (
    <span
      className={cn('inline-block h-2.5 w-2.5 rounded-sm', color.bg)}
      aria-hidden="true"
    />
  );
}

export interface LeaveBalancePanelProps {
  summary: LeaveSummaryResult;
}

export function LeaveBalancePanel({ summary }: LeaveBalancePanelProps) {
  return (
    <section className="rounded-[12px] border border-black/[0.08] bg-white dark:border-white/[0.06] dark:bg-[#1D252D]">
      <h2 className="border-b border-black/[0.06] px-5 py-4 text-base font-semibold dark:border-white/[0.06]">
        Leave balance
      </h2>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Type</TableHead>
            <TableHead className="text-right">Total</TableHead>
            <TableHead className="text-right">Taken</TableHead>
            <TableHead className="text-right">Balance</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {summary.rows.map((row) => (
            <TableRow key={row.leaveTypeId ?? '__feestdagen__'}>
              <TableCell>
                <div className="flex items-center gap-2">
                  <ColorDot leaveTypeId={row.leaveTypeId} />
                  <span>{row.name}</span>
                </div>
              </TableCell>
              <TableCell className="text-right tabular-nums">{displayVal(row.total)}</TableCell>
              <TableCell className="text-right tabular-nums">{displayVal(row.taken)}</TableCell>
              <TableCell className="text-right tabular-nums">{displayVal(row.balance)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell className="font-semibold">Total (limited)</TableCell>
            <TableCell className="text-right font-semibold tabular-nums">{displayVal(summary.totals.total)}</TableCell>
            <TableCell className="text-right font-semibold tabular-nums">{displayVal(summary.totals.taken)}</TableCell>
            <TableCell className="text-right font-semibold tabular-nums">{displayVal(summary.totals.balance)}</TableCell>
          </TableRow>
        </TableFooter>
      </Table>
    </section>
  );
}
