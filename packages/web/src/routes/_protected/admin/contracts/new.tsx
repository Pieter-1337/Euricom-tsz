import { createFileRoute, redirect } from '@tanstack/react-router';
import { ContractCreateForm } from '#/features/contracts/components/contract-create-form';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/admin/contracts/new')({
  beforeLoad: ({ context }) => {
    const currentUser = (context as { currentUser?: CurrentUser }).currentUser;
    if (!currentUser?.roles.includes(UserRole.Admin)) throw redirect({ to: '/admin/contracts' });
  },
  component: ContractCreateForm,
});
