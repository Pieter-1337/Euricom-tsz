import { createFileRoute, useRouter } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { useForm } from '@tanstack/react-form';
import { z } from 'zod';
import { useState, useCallback } from 'react';
import { getUserById, removeUser, updateUser } from '#/api/users.server';
import { USER_ROLES, UserRole, type User } from '#/api/users';
import { getUserLeaves, updateUserLeave } from '#/api/user-leaves.server';
import { LeaveAllowed } from '#/api/leave-types';
import type { UserLeave, UpdateUserLeaveRequest } from '#/api/user-leaves';
import { throwApiError, parseServerError } from '#/lib/server-error';
import { hasClientSideError } from '#/lib/form-utils';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '#/components/ui/dialog';
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

const fetchUserById = createServerFn({ method: 'GET' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: id }): Promise<User | null> => {
    return await getUserById(id);
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

const fetchUserLeaves = createServerFn({ method: 'GET' })
  .inputValidator(userIdSchema)
  .handler(async ({ data: userId }): Promise<UserLeave[]> => {
    return await getUserLeaves(userId);
  });

const submitUpdateUserLeave = createServerFn({ method: 'POST' })
  .inputValidator((input: unknown) => input as { userId: string; id: string; body: UpdateUserLeaveRequest })
  .handler(async ({ data }) => {
    try {
      await updateUserLeave(data.userId, data.id, data.body);
    } catch (e) {
      throwApiError(e);
    }
  });

export const Route = createFileRoute('/_protected/admin/users/$id')({
  loader: ({ params }) => fetchUserById({ data: params.id }),
  component: EditUser,
});

function EditUser() {
  const user = Route.useLoaderData();
  const router = useRouter();
  const [serverError, setServerError] = useState<string | null>(null);

  const form = useForm({
    defaultValues: {
      name: user?.name ?? '',
      role: (user?.role ?? UserRole.User) as UserRole,
    },
    validators: { onChange: updateUserSchema },
    onSubmit: async ({ value }) => {
      if (!user) return;
      setServerError(null);
      for (const field of ['name', 'role'] as const) {
        form.setFieldMeta(field, (prev) => ({
          ...prev,
          errorMap: { ...prev.errorMap, onServer: undefined },
        }));
      }
      try {
        await saveUser({ data: { id: user.id, user: value } });
        await router.invalidate();
      } catch (e) {
        const apiErr = parseServerError(e);
        if (apiErr) {
          const fieldErrors = apiErr.fieldErrors;
          if (fieldErrors) {
            for (const [field, errs] of Object.entries(fieldErrors)) {
              const key = (field.charAt(0).toLowerCase() + field.slice(1)) as 'name' | 'role';
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
      {serverError && (
        <p className="mt-2 text-sm text-destructive">{serverError}</p>
      )}

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
            <form.Subscribe
              selector={(state) => ({
                hasClientError: hasClientSideError(state.fieldMeta),
                isSubmitting: state.isSubmitting,
              })}
            >
              {({ hasClientError, isSubmitting }) => (
                <Button type="submit" disabled={hasClientError || isSubmitting}>
                  {isSubmitting ? 'Saving…' : 'Save'}
                </Button>
              )}
            </form.Subscribe>
            <Button
              type="button"
              variant="destructive"
              onClick={async () => {
                if (!confirm(`Delete ${user.name}?`)) return;
                try {
                  await deleteUser({ data: user.id });
                  router.navigate({ to: '/admin/users' });
                } catch (e) {
                  const apiErr = parseServerError(e);
                  setServerError(apiErr ? apiErr.userMessage : 'Something went wrong.');
                }
              }}
            >
              Delete
            </Button>
          </div>
        </form>
      </section>

      <LeavesSection userId={user.id} />
    </main>
  );
}

function LeavesSection({ userId }: { userId: string }) {
  const [leaves, setLeaves] = useState<UserLeave[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<UserLeave | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const lv = await fetchUserLeaves({ data: userId });
      setLeaves(lv);
    } finally {
      setLoading(false);
    }
  }, [userId]);

  const [loaded, setLoaded] = useState(false);
  if (!loaded) {
    setLoaded(true);
    void load();
  }

  return (
    <section className="mt-8">
      <h2 className="mb-4 text-lg font-semibold">Leaves</h2>

      {loading ? (
        <p className="text-sm text-muted-foreground">Loading…</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Allowed</TableHead>
              <TableHead>Total (days)</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {(leaves ?? []).length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="text-center text-muted-foreground">
                  No leaves
                </TableCell>
              </TableRow>
            ) : (
              (leaves ?? []).map((leave) => {
                const isUnlimited = leave.defaultAllowed === LeaveAllowed.Unlimited;
                return (
                  <TableRow key={leave.id}>
                    <TableCell>{leave.leaveTypeName}</TableCell>
                    <TableCell>
                      {isUnlimited ? (
                        <span className="rounded bg-muted px-2 py-0.5 text-xs">Unlimited</span>
                      ) : (
                        leave.defaultAllowed
                      )}
                    </TableCell>
                    <TableCell>{isUnlimited ? '—' : (leave.totalDays ?? '—')}</TableCell>
                    <TableCell className="text-right">
                      {!isUnlimited && (
                        <Button variant="outline" size="sm" onClick={() => setEditing(leave)}>
                          Edit
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      )}

      <EditLeaveDialog
        leave={editing}
        userId={userId}
        onClose={() => setEditing(null)}
        onSuccess={async () => {
          setEditing(null);
          await load();
        }}
      />
    </section>
  );
}

const editLeaveSchema = z.object({
  totalDays: z.coerce.number().min(0),
});

function EditLeaveDialog({
  leave,
  userId,
  onClose,
  onSuccess,
}: {
  leave: UserLeave | null;
  userId: string;
  onClose: () => void;
  onSuccess: () => Promise<void>;
}) {
  const isOpen = leave !== null;
  const [serverError, setServerError] = useState<string | null>(null);

  const form = useForm({
    defaultValues: {
      totalDays: leave?.totalDays ?? 0,
    },
    validators: { onChange: editLeaveSchema },
    onSubmit: async ({ value }) => {
      if (!leave) return;
      setServerError(null);
      try {
        await submitUpdateUserLeave({
          data: {
            userId,
            id: leave.id,
            body: { userId, id: leave.id, totalDays: value.totalDays },
          },
        });
        await onSuccess();
      } catch (e) {
        const apiErr = parseServerError(e);
        setServerError(apiErr ? apiErr.userMessage : 'Something went wrong.');
      }
    },
  });

  const [prevLeaveId, setPrevLeaveId] = useState<string | null>(null);
  const currentLeaveId = leave?.id ?? null;
  if (prevLeaveId !== currentLeaveId) {
    setPrevLeaveId(currentLeaveId);
    form.reset({ totalDays: leave?.totalDays ?? 0 });
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit Leave</DialogTitle>
        </DialogHeader>
        {serverError && (
          <p className="text-sm text-destructive">{serverError}</p>
        )}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            e.stopPropagation();
            form.handleSubmit();
          }}
          className="grid gap-4"
        >
          <div className="grid gap-2">
            <Label>Name</Label>
            <p className="text-sm">{leave?.leaveTypeName}</p>
          </div>

          <form.Field name="totalDays">
            {(field) => (
              <div className="grid gap-2">
                <Label htmlFor={field.name}>
                  Total <span className="text-destructive">*</span>
                </Label>
                <div className="flex items-center gap-2">
                  <Input
                    id={field.name}
                    type="number"
                    min={0}
                    value={field.state.value}
                    onBlur={field.handleBlur}
                    onChange={(e) => field.handleChange(Number(e.target.value))}
                  />
                  <span className="text-sm text-muted-foreground">days</span>
                </div>
                <FieldError field={field} />
              </div>
            )}
          </form.Field>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <form.Subscribe selector={(s) => [s.canSubmit, s.isSubmitting] as const}>
              {([canSubmit, isSubmitting]) => (
                <Button type="submit" disabled={!canSubmit}>
                  {isSubmitting ? 'Saving…' : 'Update leave'}
                </Button>
              )}
            </form.Subscribe>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
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
