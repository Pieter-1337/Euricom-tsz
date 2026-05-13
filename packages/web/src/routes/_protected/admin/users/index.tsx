import { createFileRoute, Link } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { getUsers, type User } from '#/api/users';
import { Button } from '#/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';

const fetchUsers = createServerFn({ method: 'GET' }).handler(async (): Promise<User[]> => {
  return await getUsers();
});

export const Route = createFileRoute('/_protected/admin/users/')({
  loader: () => fetchUsers(),
  component: UsersList,
});

function UsersList() {
  const users = Route.useLoaderData();

  return (
    <main>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Users</h1>
        <Button asChild>
          <Link to="/admin/users/new">New user</Link>
        </Button>
      </div>
      <Table className="mt-4">
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Role</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {users.map((u) => (
            <TableRow key={u.id}>
              <TableCell>
                <Link to="/admin/users/$id" params={{ id: u.id }} className="hover:underline">
                  {u.name}
                </Link>
              </TableCell>
              <TableCell>{u.email}</TableCell>
              <TableCell>{u.role}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </main>
  );
}
