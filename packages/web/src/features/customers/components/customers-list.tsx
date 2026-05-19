import { Link, useRouter } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import type { Customer } from '#/api/customers';
import { Button } from '#/components/ui/button';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeaderCell } from '#/components/list/sortable-header-cell';
import { useListQuery } from '#/hooks/use-list-query';
import { fetchCustomersPaged } from '#/features/customers/server-fns';
import type { CustomerSortKey } from '#/features/customers/schemas';

const columns: ColumnDef<Customer, unknown>[] = [
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
];

export function CustomersList() {
  const router = useRouter();

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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Customers</h1>
        <Button asChild>
          <Link to="/admin/customers/new">New customer</Link>
        </Button>
      </div>
      <ListShell
        search={search}
        onSearchChange={setSearch}
        total={total}
        toggle={{
          label: 'Active',
          checked: !deletedOnly,
          onChange: (checked) => setDeletedOnly(!checked),
        }}
        columns={columns}
        items={items}
        rowKey={(c) => c.id}
        sorting={sorting}
        onSortingChange={onSortingChange}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState="No customers found."
        sentinelRef={sentinelRef}
        onRowClick={(c) => void router.navigate({ to: '/admin/customers/$id', params: { id: c.id } })}
      />
    </div>
  );
}
