import { Link, useRouter } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import type { User } from '#/api/users';
import { Button } from '#/components/ui/button';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeaderCell } from '#/components/list/sortable-header-cell';
import { useListQuery } from '#/hooks/use-list-query';
import { fetchUsersPaged } from '#/features/users/server-fns';
import type { UserSortKey } from '#/features/users/schemas';

const columns: ColumnDef<User, unknown>[] = [
  {
    id: 'name' satisfies UserSortKey,
    header: ({ column }) => <SortableHeaderCell column={column} label="Name" />,
    cell: ({ row }) => `${row.original.firstName} ${row.original.lastName}`,
  },
  {
    id: 'email' satisfies UserSortKey,
    accessorKey: 'email',
    header: ({ column }) => <SortableHeaderCell column={column} label="Email" />,
  },
  {
    id: 'role' satisfies UserSortKey,
    accessorKey: 'role',
    header: ({ column }) => <SortableHeaderCell column={column} label="Role" />,
  },
];

export function UsersList() {
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
  } = useListQuery<User, UserSortKey>({
    queryKey: ['users-paged'],
    fetcher: (params) => fetchUsersPaged({ data: params }),
    defaultSort: { by: 'name', dir: 'asc' },
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Users</h1>
        <Button asChild>
          <Link to="/admin/users/new">New user</Link>
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
        rowKey={(u) => u.id}
        sorting={sorting}
        onSortingChange={onSortingChange}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState="No users found."
        sentinelRef={sentinelRef}
        onRowClick={(u) => void router.navigate({ to: '/admin/users/$id', params: { id: u.id } })}
      />
    </div>
  );
}
