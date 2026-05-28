import { createFileRoute, redirect } from '@tanstack/react-router';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/')({
  beforeLoad: ({ context }) => {
    // Admins land on My Tasks (approval queue); everyone else on Timesheets.
    // Unauthenticated requests fall through so the root layout can kick off
    // the sign-in flow (redirecting here would bounce off the _authenticated
    // guard → loop).
    const { currentUser } = context as { currentUser?: CurrentUser };
    if (!currentUser) return;
    if (currentUser.roles.includes(UserRole.Admin)) throw redirect({ to: '/my-tasks' });
    throw redirect({ to: '/timesheets' });
  },
  component: () => null,
});
