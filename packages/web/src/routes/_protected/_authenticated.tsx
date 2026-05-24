import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated')({
  beforeLoad: ({ context }) => {
    const { currentUser } = context as { currentUser?: CurrentUser };
    if (!currentUser) throw redirect({ to: '/' });
    return { currentUser };
  },
  component: () => <Outlet />,
});
