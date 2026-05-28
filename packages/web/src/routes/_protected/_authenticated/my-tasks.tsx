import { createFileRoute, redirect } from '@tanstack/react-router';
import { UserRole } from '#/api/users';
import { MyTasksList } from '#/features/my-tasks/components/my-tasks-list';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/my-tasks')({
  beforeLoad: ({ context }) => {
    const { currentUser } = context as { currentUser: CurrentUser };
    if (!currentUser.roles.includes(UserRole.Admin)) throw redirect({ to: '/' });
  },
  component: MyTasksList,
});
