import { Link, useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import { Building2 } from 'lucide-react';
import type { Customer } from '#/api/customers';
import { UserRole } from '#/api/users';
import { Button } from '#/components/ui/button';
import { EmptyState } from '#/components/list/empty-state';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeaderCell } from '#/components/list/sortable-header-cell';
import { useListQuery } from '#/hooks/use-list-query';
import { fetchCustomersPaged } from '#/features/customers/server-fns';
import { fetchUsersByRole } from '#/features/users/server-fns';
import type { CustomerSortKey } from '#/features/customers/schemas';

export function CustomersList({ canCreate }: { canCreate: boolean }) {
  const router = useRouter();

  const { data: managers = [] } = useQuery({
    queryKey: ['client-managers'],
    queryFn: () => fetchUsersByRole({ data: UserRole.ClientManager }),
  });
  const managerNameById = useMemo(
    () => new Map(managers.map((u) => [u.id, `${u.firstName} ${u.lastName}`])),
    [managers],
  );

  const columns = useMemo<ColumnDef<Customer, unknown>[]>(
    () => [
      {
        id: 'number' satisfies CustomerSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="#" />,
        cell: ({ row }) => row.original.number,
      },
      {
        id: 'name' satisfies CustomerSortKey,
        accessorKey: 'name',
        header: ({ column }) => <SortableHeaderCell column={column} label="Name" />,
      },
      {
        id: 'city' satisfies CustomerSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="City" />,
        cell: ({ row }) => row.original.address.city ?? '',
      },
      {
        id: 'contactEmail' satisfies CustomerSortKey,
        header: ({ column }) => <SortableHeaderCell column={column} label="Contact email" />,
        cell: ({ row }) => row.original.contactPerson.email,
      },
      {
        id: 'clientManager',
        header: 'Client manager',
        enableSorting: false,
        cell: ({ row }) =>
          row.original.clientManagerId ? (managerNameById.get(row.original.clientManagerId) ?? '—') : '',
      },
    ],
    [managerNameById],
  );

  const {
    items,
    total,
    search,
    setSearch,
    sorting,
    onSortingChange,
    deletedOnly,
    setDeletedOnly,
    sentinelRef,
    isLoading,
    isFetchingNextPage,
    error,
  } = useListQuery<Customer, CustomerSortKey>({
    queryKey: ['customers-paged'],
    fetcher: (params) => fetchCustomersPaged({ data: params }),
    defaultSort: { by: 'number', dir: 'asc' },
  });

  const hasFilter = search.trim() !== '' || deletedOnly;
  const emptyState = hasFilter ? (
    <EmptyState
      icon={Building2}
      title="No matches"
      description={
        deletedOnly && !search.trim()
          ? 'No deleted customers to show.'
          : 'No customers match your filters. Try a different search or clear the filters.'
      }
      action={
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setSearch('');
            setDeletedOnly(false);
          }}
        >
          Clear filters
        </Button>
      }
    />
  ) : (
    <EmptyState
      icon={Building2}
      title="No customers yet"
      description="Add your first customer to get started."
      action={
        canCreate ? (
          <Button asChild size="sm">
            <Link to="/admin/customers/new">New customer</Link>
          </Button>
        ) : undefined
      }
    />
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Customers</h1>
        {canCreate && (
          <Button asChild>
            <Link to="/admin/customers/new">New customer</Link>
          </Button>
        )}
      </div>
      <ListShell
        toolbar={{
          search,
          onSearchChange: setSearch,
          total,
          deletedFilter: { deletedOnly, onDeletedOnlyChange: setDeletedOnly },
        }}
        table={{
          columns,
          items,
          rowKey: (c) => c.id,
          sorting,
          onSortingChange,
          isLoading,
          isFetchingNextPage,
          error,
          emptyState,
          sentinelRef,
          onRowClick: (c) => void router.navigate({ to: '/admin/customers/$id', params: { id: c.id } }),
        }}
      />
    </div>
  );
}
