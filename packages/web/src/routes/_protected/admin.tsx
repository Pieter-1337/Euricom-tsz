import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import { UserRole, type User } from '#/api/users';

export const Route = createFileRoute('/_protected/admin')({
  beforeLoad: ({ context }) => {
    const currentUser = (context as any).currentUser as User | undefined;
    if (!currentUser?.roles?.includes(UserRole.Admin)) throw redirect({ to: '/' });
  },
  component: () => <Outlet />,
});
