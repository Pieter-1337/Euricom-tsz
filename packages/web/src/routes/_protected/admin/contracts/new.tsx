import { createFileRoute } from '@tanstack/react-router';
import { ContractCreateForm } from '#/features/contracts/components/contract-create-form';

export const Route = createFileRoute('/_protected/admin/contracts/new')({
  component: ContractCreateForm,
});
