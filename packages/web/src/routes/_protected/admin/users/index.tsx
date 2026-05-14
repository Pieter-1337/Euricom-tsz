import { createFileRoute, Link, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import { getUsersPaged } from '#/api/users.server';
import type { User } from '#/api/users';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';
import { Button } from '#/components/ui/button';
import { TableHead } from '#/components/ui/table';
import { ListShell } from '#/components/list/list-shell';
import { SortableHeader } from '#/components/list/sortable-header';
import { TableCell } from '#/components/ui/table';
import { useListQuery } from '#/lib/use-list-query';

type UserSortKey = 'name' | 'email' | 'role';

const getUsersPagedParamsSchema = z.object({
  search: z.string().optional(),
  sortBy: z.enum(['name', 'email', 'role']).optional(),
  sortDir: z.enum(['asc', 'desc']).optional(),
  pageSize: z.number().optional(),
  cursor: z.string().optional(),
  deletedOnly: z.boolean().optional(),
});

const fetchUsersPaged = createServerFn({ method: 'GET' })
  .inputValidator((input: unknown) => getUsersPagedParamsSchema.parse(input))
  .handler(
    async ({ data }): Promise<KeysetPage<User>> => getUsersPaged(data as KeysetQueryParams<UserSortKey>),
  );

export const Route = createFileRoute('/_protected/admin/users/')({
  component: UsersList,
});

function UsersList() {
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
