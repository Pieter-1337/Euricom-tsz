import { Link, useRouter } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { Users as UsersIcon } from 'lucide-react';
import type { User } from '#/api/users';
import { Badge } from '#/components/ui/badge';
import { Button } from '#/components/ui/button';
import { EmptyState } from '#/components/list/empty-state';
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
    id: 'roles',
    header: 'Roles',
    cell: ({ row }) => (
      <div className="flex flex-wrap gap-1">
        {row.original.roles.map((r) => (
          <Badge key={r} variant="secondary">
            {r}
          </Badge>
        ))}
      </div>
    ),
  },
];

export function UsersList() {
  const router = useRouter();

  const props = useListQuery<User, UserSortKey>({
    queryKey: ['users-paged'],
    fetcher: (params) => fetchUsersPaged({ data: params }),
    defaultSort: { by: 'name', dir: 'asc' },
  });

  const hasFilter = props.search.trim() !== '' || props.deletedOnly;
  const emptyState = hasFilter ? (
    <EmptyState
      icon={UsersIcon}
      title="No matches"
      description={
        props.deletedOnly && !props.search.trim()
          ? 'No deleted users to show.'
          : 'No users match your filters. Try a different search or clear the filters.'
      }
      action={
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            props.setSearch('');
            props.setDeletedOnly(false);
          }}
        >
          Clear filters
        </Button>
      }
    />
  ) : (
    <EmptyState
      icon={UsersIcon}
      title="No users yet"
      description="Add the first user to get started."
      action={
        <Button asChild size="sm">
          <Link to="/admin/users/new">New user</Link>
        </Button>
      }
    />
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Users</h1>
        <Button asChild>
          <Link to="/admin/users/new">New user</Link>
        </Button>
      </div>
      <ListShell
        toolbar={{
          search: props.search,
          onSearchChange: props.setSearch,
          total: props.total,
          deletedFilter: { deletedOnly: props.deletedOnly, onDeletedOnlyChange: props.setDeletedOnly },
        }}
        table={{
          columns,
          items: props.items,
          rowKey: (u) => u.id,
          sorting: props.sorting,
          onSortingChange: props.onSortingChange,
          isLoading: props.isLoading,
          isFetchingNextPage: props.isFetchingNextPage,
          error: props.error,
          emptyState,
          sentinelRef: props.sentinelRef,
          onRowClick: (u) => void router.navigate({ to: '/admin/users/$id', params: { id: u.id } }),
        }}
      />
    </div>
  );
}
