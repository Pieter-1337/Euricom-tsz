import { createFileRoute } from '@tanstack/react-router';
import { CustomersList } from '#/features/customers/components/customers-list';

export const Route = createFileRoute('/_protected/admin/customers/')({
  component: CustomersList,
});
