import { createFileRoute } from '@tanstack/react-router';
import { ContractsList } from '#/features/contracts/components/contracts-list';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/admin/contracts/')({
  component: ContractsListPage,
});

function ContractsListPage() {
  const { currentUser } = Route.useRouteContext() as { currentUser: CurrentUser };
  const canCreate = currentUser.roles.includes(UserRole.Admin);
  return <ContractsList canCreate={canCreate} />;
}
