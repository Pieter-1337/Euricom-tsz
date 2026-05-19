import { createFileRoute } from '@tanstack/react-router';
import { CustomerCreateForm } from '#/features/customers/components/customer-create-form';

export const Route = createFileRoute('/_protected/admin/customers/new')({
  component: CustomerCreateForm,
});
