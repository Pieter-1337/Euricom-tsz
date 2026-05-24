import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import { UserRole, type User } from '#/api/users';

export const Route = createFileRoute('/_protected/_authenticated/customers')({
  beforeLoad: ({ context }) => {
    const currentUser = (context as any).currentUser as User | undefined;
    const roles = currentUser?.roles ?? [];
    const hasAccess = roles.includes(UserRole.Admin) || roles.includes(UserRole.ClientManager);
    if (!hasAccess) throw redirect({ to: '/' });
  },
  component: () => <Outlet />,
});
