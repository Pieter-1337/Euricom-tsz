import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/_protected/admin')({
  beforeLoad: ({ context }) => {
    const currentUser = (context as any).currentUser as { role?: string } | undefined;
    if (currentUser?.role !== 'Admin') throw redirect({ to: '/' });
  },
  component: () => <Outlet />,
});
