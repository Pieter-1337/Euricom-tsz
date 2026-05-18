import { Link, useRouter } from '@tanstack/react-router';
import type { User } from '#/api/users';
import { Button } from '#/components/ui/button';
import { TableCell, TableHead } from '#/components/ui/table';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeader } from '#/components/list/sortable-header';
import { useListQuery } from '#/hooks/use-list-query';
import { fetchUsersPaged } from '#/features/users/server-fns';
import type { UserSortKey } from '#/features/users/schemas';

export function UsersList() {
  const router = useRouter();

  const { items, total, search, setSearch, sortBy, sortDir, setSort, deletedOnly, setDeletedOnly, sentinelRef, isLoading, isFetchingNextPage, error } =
    useListQuery<User, UserSortKey>({
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
        items={items}
        rowKey={(u) => u.id}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState="No users found."
        sentinelRef={sentinelRef}
        onRowClick={(u) => void router.navigate({ to: '/admin/users/$id', params: { id: u.id } })}
      >
        {{
          head: (
            <>
              <TableHead>
                <SortableHeader<UserSortKey>
                  label="Name"
                  sortKey="name"
                  currentSortBy={sortBy}
                  currentSortDir={sortDir}
                  onSort={setSort}
                />
              </TableHead>
              <TableHead>
                <SortableHeader<UserSortKey>
                  label="Email"
                  sortKey="email"
                  currentSortBy={sortBy}
                  currentSortDir={sortDir}
                  onSort={setSort}
                />
              </TableHead>
              <TableHead>
                <SortableHeader<UserSortKey>
                  label="Role"
                  sortKey="role"
                  currentSortBy={sortBy}
                  currentSortDir={sortDir}
                  onSort={setSort}
                />
              </TableHead>
            </>
          ),
          row: (u) => (
            <>
              <TableCell>
                {u.firstName} {u.lastName}
              </TableCell>
              <TableCell>{u.email}</TableCell>
              <TableCell>{u.role}</TableCell>
            </>
          ),
        }}
      </ListShell>
    </div>
  );
}
