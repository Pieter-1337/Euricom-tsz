import { createFileRoute } from '@tanstack/react-router';
import { fetchContract } from '#/features/contracts/server-fns';
import { ContractEditForm } from '#/features/contracts/components/contract-edit-form';
import type { CurrentUser } from '#/server/current-user';

export const Route = createFileRoute('/_protected/admin/contracts/$contractId')({
  loader: ({ params }) => fetchContract({ data: params.contractId }),
  component: EditContractPage,
});

function EditContractPage() {
  const contract = Route.useLoaderData();
  const { currentUser } = Route.useRouteContext() as { currentUser: CurrentUser };

  if (!contract) {
    return (
      <main>
        <h1 className="text-2xl font-bold">Contract not found</h1>
      </main>
    );
  }

  return <ContractEditForm contract={contract} currentUser={currentUser} />;
}
