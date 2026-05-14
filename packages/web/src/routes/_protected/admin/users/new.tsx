import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useForm } from '@tanstack/react-form';
import { z } from 'zod';
import { useState } from 'react';
import { createUser } from '#/api/users.server';
import { USER_ROLES, UserRole } from '#/api/users';
import { throwApiError, parseServerError } from '#/lib/server-error';
import { hasClientSideError } from '#/lib/form-utils';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';

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
  const [serverError, setServerError] = useState<string | null>(null);

  const form = useForm({
    defaultValues: {
      name: '',
      email: '',
      role: UserRole.User as UserRole,
    },
    validators: { onChange: createUserSchema },
    onSubmit: async ({ value }) => {
      setServerError(null);
      for (const field of ['name', 'email', 'role'] as const) {
        form.setFieldMeta(field, (prev) => ({
          ...prev,
          errorMap: { ...prev.errorMap, onServer: undefined },
        }));
      }
      try {
        await submitCreateUser({ data: value });
        await router.invalidate();
        router.navigate({ to: '/admin/users' });
      } catch (e) {
        const apiErr = parseServerError(e);
        if (apiErr) {
          const fieldErrors = apiErr.fieldErrors;
          if (fieldErrors) {
            for (const [field, errs] of Object.entries(fieldErrors)) {
              const key = (field.charAt(0).toLowerCase() + field.slice(1)) as 'name' | 'email' | 'role';
              form.setFieldMeta(key, (prev) => ({
                ...prev,
                errorMap: { ...prev.errorMap, onServer: errs.map((fe) => fe.message) },
                isTouched: true,
              }));
            }
          }
          setServerError(apiErr.userMessage);
        } else {
          setServerError('Something went wrong.');
        }
      }
    },
  });

  return (
    <main>
      <h1 className="text-2xl font-bold">New user</h1>
      {serverError && (
        <p className="mt-2 text-sm text-destructive">{serverError}</p>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          event.stopPropagation();
          form.handleSubmit();
        }}
        className="mt-4 grid max-w-md gap-4"
      >
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

        <form.Field name="email">
          {(field) => (
            <div className="grid gap-2">
              <Label htmlFor={field.name}>Email</Label>
              <Input
                id={field.name}
                name={field.name}
                type="email"
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

        <form.Subscribe
          selector={(state) => ({
            hasClientError: hasClientSideError(state.fieldMeta),
            isSubmitting: state.isSubmitting,
          })}
        >
          {({ hasClientError, isSubmitting }) => (
            <div>
              <Button type="submit" disabled={hasClientError || isSubmitting}>
                {isSubmitting ? 'Creating…' : 'Create user'}
              </Button>
            </div>
          )}
        </form.Subscribe>
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
