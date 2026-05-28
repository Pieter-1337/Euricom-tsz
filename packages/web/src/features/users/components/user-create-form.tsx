import { useRouter } from '@tanstack/react-router';
import { SELECTABLE_ROLES, UserRole } from '#/api/users';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { createUserSchema } from '#/features/users/schemas';
import { submitCreateUser } from '#/features/users/server-fns';

export function UserCreateForm() {
  const router = useRouter();

  const form = useAppForm({
    defaultValues: {
      firstName: '',
      lastName: '',
      email: '',
      roles: [] as UserRole[],
    },
    validators: { onChange: createUserSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        const created = await submitCreateUser({
          data: { ...value, roles: [UserRole.User, ...value.roles] },
        });
        await router.invalidate();
        if (created) router.navigate({ to: '/admin/users/$id', params: { id: created.id } });
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, [
    'firstName',
    'lastName',
    'email',
    'roles',
  ]);

  return (
    <main>
      <h1 className="text-2xl font-bold">New user</h1>
      <form.AppForm>
        <form.FormErrorBanner message={serverError} />
        <form
          onSubmit={(event) => {
            event.preventDefault();
            event.stopPropagation();
            form.handleSubmit();
          }}
          className="mt-4 grid max-w-md gap-4"
        >
          <form.AppField name="firstName">{(field) => <field.TextField label="First name" />}</form.AppField>

          <form.AppField name="lastName">{(field) => <field.TextField label="Last name" />}</form.AppField>

          <form.AppField name="email">{(field) => <field.TextField label="Email" type="email" />}</form.AppField>

          <form.AppField name="roles">
            {(field) => <field.MultiSelectField label="Roles" options={SELECTABLE_ROLES} />}
          </form.AppField>

          <form.FormActions saveLabel="Create user" savePendingLabel="Creating…" />
        </form>
      </form.AppForm>
    </main>
  );
}
