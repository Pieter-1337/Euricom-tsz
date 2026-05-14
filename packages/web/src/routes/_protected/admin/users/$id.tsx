import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import { getUserById, removeUser, updateUser } from '#/api/users.server';
import { USER_ROLES, UserRole } from '#/api/users';
import { getUserLeaves, updateUserLeaves } from '#/api/user-leaves.server';
import { LeaveAllowed, type UserLeave, type UpdateUserLeavesBody } from '#/api/user-leaves';
import { throwApiError } from '#/lib/server-error';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/lib/use-form-server-errors';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';

const userIdSchema = z.string().min(1);

const updateUserSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  role: z.enum(USER_ROLES),
});

const saveUserInputSchema = z.object({
  id: userIdSchema,
  user: updateUserSchema,
});

const leavesFormSchema = z.object({
  items: z.array(
    z.object({
      id: z.string(),
      leaveTypeId: z.string(),
      totalDays: z.coerce.number().min(0).nullable(),
    }),
  ),
});

const fetchUserAndLeaves = createServerFn({ method: 'GET' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }) => {
    const [user, leaves] = await Promise.all([getUserById(id), getUserLeaves(id)]);
    return { user, leaves };
  });

const saveUser = createServerFn({ method: 'POST' })
  .inputValidator(saveUserInputSchema)
  .handler(async ({ data }) => {
    try {
      await updateUser(data.id, { id: data.id, name: data.user.name, role: data.user.role });
    } catch (e) {
      throwApiError(e);
    }
  });

const deleteUser = createServerFn({ method: 'POST' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }) => {
    try {
      await removeUser(id);
    } catch (e) {
      throwApiError(e);
    }
  });

const submitUpdateUserLeaves = createServerFn({ method: 'POST' })
  .inputValidator((input: unknown) => input as { userId: string; body: UpdateUserLeavesBody })
  .handler(async ({ data }): Promise<UserLeave[]> => {
    try {
      return await updateUserLeaves(data.userId, data.body);
    } catch (e) {
      throwApiError(e);
    }
  });

export const Route = createFileRoute('/_protected/admin/users/$id')({
  loader: ({ params }) => fetchUserAndLeaves({ data: params.id }),
  component: EditUser,
});

function EditUser() {
  const { user, leaves } = Route.useLoaderData();
  const router = useRouter();

  const form = useAppForm({
    defaultValues: {
      name: user?.name ?? '',
      role: (user?.role ?? UserRole.User) as UserRole,
    },
    validators: { onChange: updateUserSchema },
    onSubmit: async ({ value }) => {
      if (!user) return;
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

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, ['name', 'role']);

  if (!user) {
    return (
      <main>
        <h1 className="text-2xl font-bold">User not found</h1>
      </main>
    );
  }

  return (
    <main>
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{user.name}</h1>
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={async () => {
            if (!confirm(`Delete ${user.name}?`)) return;
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

            <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>

            <form.AppField name="role">
              {(field) => <field.SelectField label="Role" options={USER_ROLES} />}
            </form.AppField>

            <form.FormActions cancel />
          </form>
        </section>
      </form.AppForm>

      <LeaveOverviewSection userId={user.id} leaves={leaves} />
    </main>
  );
}

function LeaveOverviewSection({ userId, leaves }: { userId: string; leaves: UserLeave[] }) {
  const router = useRouter();
  const currentYear = new Date().getFullYear();

  const toFormItems = (rows: UserLeave[]) =>
    rows.map((l) => ({ id: l.id, leaveTypeId: l.leaveTypeId, totalDays: l.totalDays }));

  const form = useAppForm({
    defaultValues: { items: toFormItems(leaves) },
    validators: { onChange: leavesFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        const updated = await submitUpdateUserLeaves({
          data: { userId, body: { year: currentYear, items: value.items } },
        });
        form.reset({ items: toFormItems(updated) });
        await router.invalidate();
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, ['items']);

  return (
    <section className="mt-8">
      <h2 className="mb-4 text-lg font-semibold">Leave overview — {currentYear}</h2>
      <form.AppForm>
        <form.FormErrorBanner message={serverError} />
        <form
          onSubmit={(e) => {
            e.preventDefault();
            e.stopPropagation();
            form.handleSubmit();
          }}
        >
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Total</TableHead>
                <TableHead>Taken</TableHead>
                <TableHead>Balance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {leaves.map((l, i) => (
                <TableRow key={l.id}>
                  <TableCell>{l.leaveTypeName}</TableCell>
                  <TableCell>
                    {l.defaultAllowed === LeaveAllowed.Unlimited ? (
                      <span className="text-muted-foreground">Unlimited</span>
                    ) : (
                      <form.AppField name={`items[${i}].totalDays`}>
                        {(field) => <field.NumberField label="" min={0} />}
                      </form.AppField>
                    )}
                  </TableCell>
                  <TableCell>—</TableCell>
                  <TableCell>—</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <form.FormActions cancel />
        </form>
      </form.AppForm>
    </section>
  );
}
