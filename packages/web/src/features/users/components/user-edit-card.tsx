import { useRouter } from '@tanstack/react-router';
import { USER_ROLES, UserRole, type User } from '#/api/users';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { updateUserSchema } from '#/features/users/schemas';
import { deleteUser, saveUser } from '#/features/users/server-fns';

export function UserEditCard({ user }: { user: User }) {
  const router = useRouter();

  const form = useAppForm({
    defaultValues: {
      firstName: user.firstName,
      lastName: user.lastName,
      role: user.role as UserRole,
    },
    validators: { onChange: updateUserSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        await saveUser({ data: { id: user.id, user: value } });
        await router.invalidate();
        form.reset(value);
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, [
    'firstName',
    'lastName',
    'role',
  ]);

  return (
    <>
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">
          {user.firstName} {user.lastName}
        </h1>
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={async () => {
            if (!confirm(`Delete ${user.firstName} ${user.lastName}?`)) return;
            try {
              await deleteUser({ data: user.id });
              router.navigate({ to: '/admin/users' });
            } catch (e) {
              handleApiError(e);
            }
          }}
        >
          Delete
        </Button>
      </div>
      <form.AppForm>
        <form.FormErrorBanner message={serverError} />

        <section className="mt-6">
          <h2 className="mb-4 text-lg font-semibold">General</h2>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              event.stopPropagation();
              form.handleSubmit();
            }}
            className="grid max-w-md gap-4"
          >
            <div className="grid gap-2">
              <Label htmlFor="email">Email</Label>
              <Input id="email" value={user.email} disabled />
            </div>

            <form.AppField name="firstName">{(field) => <field.TextField label="First name" />}</form.AppField>

            <form.AppField name="lastName">{(field) => <field.TextField label="Last name" />}</form.AppField>

            <form.AppField name="role">
              {(field) => <field.SelectField label="Role" options={USER_ROLES} />}
            </form.AppField>

            <form.FormActions cancel />
          </form>
        </section>
      </form.AppForm>
    </>
  );
}
