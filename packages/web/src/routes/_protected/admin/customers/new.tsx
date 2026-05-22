import { createFileRoute, redirect } from '@tanstack/react-router';
import { CustomerCreateForm } from '#/features/customers/components/customer-create-form';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/admin/customers/new')({
  beforeLoad: ({ context }) => {
    const currentUser = (context as { currentUser?: CurrentUser }).currentUser;
    if (!currentUser?.roles.includes(UserRole.Admin)) throw redirect({ to: '/admin/customers' });
  },
  component: CustomerCreateForm,
});
