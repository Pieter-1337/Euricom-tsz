import { createFileRoute } from '@tanstack/react-router';
import { CustomersList } from '#/features/customers/components/customers-list';
import { UserRole } from '#/api/users';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/_authenticated/customers/')({
  component: CustomersListPage,
});

function CustomersListPage() {
  const { currentUser } = Route.useRouteContext() as { currentUser: CurrentUser };
  const canCreate = currentUser.roles.includes(UserRole.Admin);
  return <CustomersList canCreate={canCreate} />;
}
