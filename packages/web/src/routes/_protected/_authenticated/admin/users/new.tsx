import { createFileRoute } from '@tanstack/react-router';
import { UserCreateForm } from '#/features/users/components/user-create-form';

export const Route = createFileRoute('/_protected/_authenticated/admin/users/new')({
  component: UserCreateForm,
});
