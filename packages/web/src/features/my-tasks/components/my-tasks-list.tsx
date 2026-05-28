import { Link } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { Inbox } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Badge } from '#/components/ui/badge';
import { Button } from '#/components/ui/button';
import { DatePicker } from '#/components/ui/date-picker';
import { Label } from '#/components/ui/label';
import { EmptyState } from '#/components/list/empty-state';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeaderCell } from '#/components/list/sortable-header-cell';
import { useListQuery } from '#/hooks/use-list-query';
import type { PendingApproval } from '#/api/timesheets';
import { fetchPendingApprovals } from '#/features/timesheets/server-fns';
import { formatIsoDate, isoWeekToMonday } from '#/features/timesheets/iso-week';
import type { PendingApprovalSortKey, PendingApprovalsExtraParams } from '#/features/my-tasks/schemas';

function weekDateRange(year: number, week: number): string {
  const monday = isoWeekToMonday(year, week);
  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);
  return `${formatIsoDate(monday)} – ${formatIsoDate(sunday)}`;
}

const columns: ColumnDef<PendingApproval, unknown>[] = [
  {
    id: 'type',
    header: 'Type',
    cell: () => <Badge variant="secondary">Approve timesheet</Badge>,
  },
  {
    id: 'employee' satisfies PendingApprovalSortKey,
    header: ({ column }) => <SortableHeaderCell column={column} label="Employee" />,
    cell: ({ row }) => <span className="font-medium">{row.original.userName}</span>,
  },
  {
    id: 'week' satisfies PendingApprovalSortKey,
    header: ({ column }) => <SortableHeaderCell column={column} label="ISO week" />,
    cell: ({ row }) => `${row.original.isoYear}-W${String(row.original.isoWeek).padStart(2, '0')}`,
  },
  {
    id: 'dateRange',
    header: 'Date range',
    cell: ({ row }) => (
      <span className="text-muted-foreground">{weekDateRange(row.original.isoYear, row.original.isoWeek)}</span>
    ),
  },
  {
    id: 'totalHours' satisfies PendingApprovalSortKey,
    header: ({ column }) => <SortableHeaderCell column={column} label="Total hours" />,
    cell: ({ row }) => <span className="tabular-nums">{row.original.totalHours}h</span>,
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => (
      <Button asChild variant="outline" size="sm">
        <Link
          to="/time-entry/week/$year/$week"
          params={{
            year: String(row.original.isoYear),
            week: String(row.original.isoWeek),
          }}
          search={{ userId: row.original.userId }}
        >
          Open
        </Link>
      </Button>
    ),
  },
];

export function MyTasksList() {
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const extraParams = useMemo<PendingApprovalsExtraParams>(
    () => ({
      dateFrom: dateFrom || undefined,
      dateTo: dateTo || undefined,
    }),
    [dateFrom, dateTo],
  );

  const list = useListQuery<PendingApproval, PendingApprovalSortKey, PendingApprovalsExtraParams>({
    queryKey: ['pending-approvals-paged'],
    fetcher: (params) => fetchPendingApprovals({ data: params }),
    defaultSort: { by: 'week', dir: 'asc' },
    extraParams,
  });

  const hasExtraFilters = dateFrom !== '' || dateTo !== '';
  const clearExtraFilters = () => {
    setDateFrom('');
    setDateTo('');
  };
  const hasAnyFilter = hasExtraFilters || list.search.trim() !== '';
  const clearAll = () => {
    clearExtraFilters();
    list.setSearch('');
  };

  const emptyState = hasAnyFilter ? (
    <EmptyState
      icon={Inbox}
      title="No matches"
      description="No tasks match your filters. Try a different search or clear the filters."
      action={
        <Button variant="outline" size="sm" onClick={clearAll}>
          Clear filters
        </Button>
      }
    />
  ) : (
    <EmptyState
      icon={Inbox}
      title="No pending approvals"
      description="All submitted timesheets have been reviewed. Check back later."
    />
  );

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">My Tasks</h1>
      <ListShell
        toolbar={{
          search: list.search,
          onSearchChange: list.setSearch,
          total: list.total,
        }}
        actions={
          <>
            <div className="grid gap-1">
              <Label htmlFor="my-tasks-date-from" className="sr-only">
                From
              </Label>
              <DatePicker
                id="my-tasks-date-from"
                value={dateFrom}
                onChange={(value) => setDateFrom(value)}
                placeholder="From"
                className="w-40"
              />
            </div>
            <div className="grid gap-1">
              <Label htmlFor="my-tasks-date-to" className="sr-only">
                To
              </Label>
              <DatePicker
                id="my-tasks-date-to"
                value={dateTo}
                onChange={(value) => setDateTo(value)}
                placeholder="To"
                className="w-40"
              />
            </div>
            {hasExtraFilters && (
              <Button variant="ghost" onClick={clearExtraFilters}>
                Clear filters
              </Button>
            )}
          </>
        }
        table={{
          columns,
          items: list.items,
          rowKey: (a) => `${a.userId}-${a.isoYear}-${a.isoWeek}`,
          sorting: list.sorting,
          onSortingChange: list.onSortingChange,
          isLoading: list.isLoading,
          isFetchingNextPage: list.isFetchingNextPage,
          error: list.error,
          emptyState,
          sentinelRef: list.sentinelRef,
        }}
      />
    </div>
  );
}
