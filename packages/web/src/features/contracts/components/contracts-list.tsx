import { Link, useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { FileText } from 'lucide-react';
import type { ContractSortKey, ContractSummary, ContractsListExtraParams } from '#/api/contracts';
import { Button } from '#/components/ui/button';
import { Combobox } from '#/components/ui/combobox';
import { DatePicker } from '#/components/ui/date-picker';
import { Label } from '#/components/ui/label';
import { EmptyState } from '#/components/list/empty-state';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeaderCell } from '#/components/list/sortable-header-cell';
import { useListQuery } from '#/hooks/use-list-query';
import { fetchContractsPaged } from '#/features/contracts/server-fns';
import { fetchAllCustomers } from '#/features/customers/server-fns';

const dateFmt = new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'short', day: '2-digit' });
const formatDate = (iso: string | null) => (iso ? dateFmt.format(new Date(iso)) : '');

export function ContractsList({ canCreate }: { canCreate: boolean }) {
  const router = useRouter();
  const [activeOnDate, setActiveOnDate] = useState('');
  const [customerId, setCustomerId] = useState('');

  const { data: customers = [] } = useQuery({
    queryKey: ['customers', 'all'],
    queryFn: () => fetchAllCustomers(),
  });
  const customerNameById = useMemo(() => new Map(customers.map((c) => [c.id, c.name] as const)), [customers]);
  const customerOptions = useMemo(
    () => customers.map((c) => ({ value: c.id, label: `${c.number} — ${c.name}` })),
    [customers],
  );

  const extraParams = useMemo<ContractsListExtraParams>(
    () => ({
      activeOnDate: activeOnDate || undefined,
      customerId: customerId || undefined,
    }),
    [activeOnDate, customerId],
  );

  const list = useListQuery<ContractSummary, ContractSortKey, ContractsListExtraParams>({
    queryKey: ['contracts-paged'],
    fetcher: (params) => fetchContractsPaged({ data: params }),
    defaultSort: { by: 'number', dir: 'asc' },
    extraParams,
  });

  const hasExtraFilters = activeOnDate !== '' || customerId !== '';
  const clearFilters = () => {
    setActiveOnDate('');
    setCustomerId('');
  };
  const hasAnyFilter = hasExtraFilters || list.search.trim() !== '' || list.deletedOnly;
  const clearAll = () => {
    clearFilters();
    list.setSearch('');
    list.setDeletedOnly(false);
  };
  const emptyState = hasAnyFilter ? (
    <EmptyState
      icon={FileText}
      title="No matches"
      description="No contracts match your filters. Try a different search or clear the filters."
      action={
        <Button variant="outline" size="sm" onClick={clearAll}>
          Clear filters
        </Button>
      }
    />
  ) : (
    <EmptyState
      icon={FileText}
      title="No contracts yet"
      description="Create a contract once you have at least one customer."
      action={
        canCreate ? (
          <Button asChild size="sm">
            <Link to="/admin/contracts/new">New contract</Link>
          </Button>
        ) : undefined
      }
    />
  );

  const columns = useMemo<ColumnDef<ContractSummary, unknown>[]>(
    () => [
      {
        id: 'number' satisfies ContractSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="#" />,
        cell: ({ row }) => row.original.number,
      },
      {
        id: 'subject' satisfies ContractSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="Subject" />,
        cell: ({ row }) => row.original.subject,
      },
      {
        id: 'customer',
        header: 'Customer',
        enableSorting: false,
        cell: ({ row }) => customerNameById.get(row.original.customerId) ?? '—',
      },
      {
        id: 'start' satisfies ContractSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="Start" />,
        cell: ({ row }) => formatDate(row.original.start),
      },
      {
        id: 'end',
        header: 'End',
        enableSorting: false,
        cell: ({ row }) => formatDate(row.original.end),
      },
      {
        id: 'activeTaskCount',
        header: 'Active tasks',
        enableSorting: false,
        cell: ({ row }) => row.original.activeTaskCount,
      },
      {
        id: 'consultantCount',
        header: 'Consultants',
        enableSorting: false,
        cell: ({ row }) => row.original.consultantCount,
      },
    ],
    [customerNameById],
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Contracts</h1>
        {canCreate && (
          <Button asChild>
            <Link to="/admin/contracts/new">New contract</Link>
          </Button>
        )}
      </div>
      <ListShell
        toolbar={{
          search: list.search,
          onSearchChange: list.setSearch,
          total: list.total,
          deletedFilter: { deletedOnly: list.deletedOnly, onDeletedOnlyChange: list.setDeletedOnly },
        }}
        actions={
          <>
            <div className="grid gap-1">
              <Label htmlFor="contracts-active-on" className="sr-only">
                Active on
              </Label>
              <DatePicker
                id="contracts-active-on"
                value={activeOnDate}
                onChange={(value) => setActiveOnDate(value)}
                placeholder="Active on"
                className="w-44"
              />
            </div>
            <div className="grid w-72 gap-1">
              <Label htmlFor="contracts-customer" className="sr-only">
                Customer
              </Label>
              <Combobox
                id="contracts-customer"
                value={customerId}
                onValueChange={setCustomerId}
                options={customerOptions}
                placeholder="All customers"
                emptyMessage="No customers found."
              />
            </div>
            {hasExtraFilters && (
              <Button variant="ghost" onClick={clearFilters}>
                Clear filters
              </Button>
            )}
          </>
        }
        table={{
          columns,
          items: list.items,
          rowKey: (c) => c.id,
          sorting: list.sorting,
          onSortingChange: list.onSortingChange,
          isLoading: list.isLoading,
          isFetchingNextPage: list.isFetchingNextPage,
          error: list.error,
          emptyState,
          sentinelRef: list.sentinelRef,
          onRowClick: (c) => void router.navigate({ to: '/admin/contracts/$contractId', params: { contractId: c.id } }),
        }}
      />
    </div>
  );
}
