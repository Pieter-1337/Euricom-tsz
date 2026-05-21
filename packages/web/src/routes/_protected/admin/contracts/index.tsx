import { createFileRoute } from '@tanstack/react-router';
import { ContractsList } from '#/features/contracts/components/contracts-list';

export const Route = createFileRoute('/_protected/admin/contracts/')({
  component: ContractsList,
});
