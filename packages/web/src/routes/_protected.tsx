import { createFileRoute, Outlet } from '@tanstack/react-router';
import type { SessionUser } from '#/lib/auth.functions';

export const Route = createFileRoute('/_protected')({
  beforeLoad: ({ context }) => {
    const session = (context as any).session as { user: SessionUser } | null;
    if (!session) return;
    return { user: session.user };
  },
  component: () => <Outlet />,
});
