import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/admin')({
  beforeLoad: ({ context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    if (!currentUser.roles.includes(UserRole.Admin)) throw redirect({ to: '/' });
  },
  component: () => <Outlet />,
});
