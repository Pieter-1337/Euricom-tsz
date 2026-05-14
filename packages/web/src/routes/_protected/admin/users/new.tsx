import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import { createUser } from '#/api/users.server';
import { USER_ROLES, UserRole } from '#/api/users';
import { throwApiError } from '#/lib/server-error';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/lib/use-form-server-errors';

const createUserSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  email: z.string().min(1, 'Email is required').email('Must be a valid email'),
  role: z.enum(USER_ROLES),
});

const submitCreateUser = createServerFn({ method: 'POST' })
  .inputValidator(createUserSchema)
  .handler(async ({ data }) => {
    try {
      return await createUser(data);
    } catch (e) {
      throwApiError(e);
    }
  });

export const Route = createFileRoute('/_protected/admin/users/new')({ component: NewUser });

function NewUser() {
  const router = useRouter();

  const form = useAppForm({
    defaultValues: {
      name: '',
      email: '',
      role: UserRole.User as UserRole,
    },
    validators: { onChange: createUserSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        const created = await submitCreateUser({ data: value });
        await router.invalidate();
        if (created) router.navigate({ to: '/admin/users/$id', params: { id: created.id } });
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, ['name', 'email', 'role']);

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
          <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>

          <form.AppField name="email">{(field) => <field.TextField label="Email" type="email" />}</form.AppField>

          <form.AppField name="role">
            {(field) => <field.SelectField label="Role" options={USER_ROLES} />}
          </form.AppField>

          <form.FormActions saveLabel="Create user" savePendingLabel="Creating…" />
        </form>
      </form.AppForm>
    </main>
  );
}
