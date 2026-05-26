import { createFileRoute, redirect } from '@tanstack/react-router';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/')({
  beforeLoad: ({ context }) => {
    // Authenticated users start on Timesheets. Unauthenticated requests fall
    // through so the root layout can kick off the sign-in flow (redirecting
    // to /timesheets here would bounce off the _authenticated guard → loop).
    const { currentUser } = context as { currentUser?: CurrentUser };
    if (currentUser) throw redirect({ to: '/timesheets' });
  },
  component: () => null,
});
