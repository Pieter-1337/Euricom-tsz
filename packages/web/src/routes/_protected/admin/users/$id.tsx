import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useForm } from '@tanstack/react-form';
import { z } from 'zod';
import { getUserById, removeUser, updateUser } from '#/api/users.server';
import { USER_ROLES, UserRole, type User } from '#/api/users';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';

const userIdSchema = z.string().min(1);

const updateUserSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  role: z.enum(USER_ROLES),
});

const saveUserInputSchema = z.object({
  id: userIdSchema,
  user: updateUserSchema,
});

const fetchUserById = createServerFn({ method: 'GET' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }): Promise<User | null> => {
    return await getUserById(id);
  });

const saveUser = createServerFn({ method: 'POST' })
  .inputValidator(saveUserInputSchema)
  .handler(async ({ data }) => {
    await updateUser(data.id, { id: data.id, name: data.user.name, role: data.user.role });
  });

const deleteUser = createServerFn({ method: 'POST' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }) => {
    await removeUser(id);
  });

export const Route = createFileRoute('/_protected/admin/users/$id')({
  loader: ({ params }) => fetchUserById({ data: params.id }),
  component: EditUser,
});

function EditUser() {
  const user = Route.useLoaderData();
  const router = useRouter();

  const form = useForm({
    defaultValues: {
      name: user?.name ?? '',
      role: (user?.role ?? UserRole.User) as UserRole,
    },
    validators: { onChange: updateUserSchema },
    onSubmit: async ({ value }) => {
      if (!user) return;
      await saveUser({ data: { id: user.id, user: value } });
      await router.invalidate();
    },
  });

  if (!user) {
    return (
      <main>
        <h1 className="text-2xl font-bold">User not found</h1>
      </main>
    );
  }

  return (
    <main>
      <h1 className="text-2xl font-bold">{user.name}</h1>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          event.stopPropagation();
          form.handleSubmit();
        }}
        className="mt-4 grid max-w-md gap-4"
      >
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" value={user.email} disabled />
        </div>

        <form.Field name="name">
          {(field) => (
            <div className="grid gap-2">
              <Label htmlFor={field.name}>Name</Label>
              <Input
                id={field.name}
                name={field.name}
                value={field.state.value}
                onBlur={field.handleBlur}
                onChange={(e) => field.handleChange(e.target.value)}
              />
              <FieldError field={field} />
            </div>
          )}
        </form.Field>

        <form.Field name="role">
          {(field) => (
            <div className="grid gap-2">
              <Label htmlFor={field.name}>Role</Label>
              <select
                id={field.name}
                name={field.name}
                value={field.state.value}
                onBlur={field.handleBlur}
                onChange={(e) => field.handleChange(e.target.value as UserRole)}
                className="border-input file:text-foreground placeholder:text-muted-foreground selection:bg-primary selection:text-primary-foreground dark:bg-input/30 flex h-9 w-full min-w-0 rounded-md border bg-transparent px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] md:text-sm"
              >
                {USER_ROLES.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
              <FieldError field={field} />
            </div>
          )}
        </form.Field>

        <div className="flex items-center gap-3">
          <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting] as const}>
            {([canSubmit, isSubmitting]) => (
              <Button type="submit" disabled={!canSubmit}>
                {isSubmitting ? 'Saving…' : 'Save'}
              </Button>
            )}
          </form.Subscribe>
          <Button
            type="button"
            variant="destructive"
            onClick={async () => {
              if (!confirm(`Delete ${user.name}?`)) return;
              await deleteUser({ data: user.id });
              router.navigate({ to: '/admin/users' });
            }}
          >
            Delete
          </Button>
        </div>
      </form>
    </main>
  );
}

function FieldError({ field }: { field: { state: { meta: { isTouched: boolean; errors: Array<unknown> } } } }) {
  if (!field.state.meta.isTouched || field.state.meta.errors.length === 0) return null;
  const message = field.state.meta.errors
    .map((err) => (typeof err === 'string' ? err : (err as { message?: string })?.message))
    .filter(Boolean)
    .join(', ');
  if (!message) return null;
  return <p className="text-sm text-destructive">{message}</p>;
}
