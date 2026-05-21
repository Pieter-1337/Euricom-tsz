import { useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import type { Contract } from '#/api/contracts';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { UserRole } from '#/api/users';
import { contractFormSchema, type ContractFormValues } from '#/features/contracts/schemas';
import { deleteContract, submitUpdateContract } from '#/features/contracts/server-fns';
import { fetchAllUsers, fetchUsersByRole } from '#/features/users/server-fns';
import { fetchAllCustomers } from '#/features/customers/server-fns';
import { ContractTaskSubform } from '#/features/contracts/components/contract-task-subform';

const FIELD_PATHS: (keyof ContractFormValues)[] = [
  'subject',
  'customerId',
  'clientManagerId',
  'start',
  'end',
  'consultantIds',
  'tasks',
];

export function ContractEditForm({ contract }: { contract: Contract }) {
  const router = useRouter();

  const { data: managers = [] } = useQuery({
    queryKey: ['client-managers'],
    queryFn: () => fetchUsersByRole({ data: UserRole.ClientManager }),
  });
  const { data: candidates = [] } = useQuery({
    queryKey: ['users', 'all'],
    queryFn: () => fetchAllUsers(),
  });
  const { data: customers = [] } = useQuery({
    queryKey: ['customers', 'all'],
    queryFn: () => fetchAllCustomers(),
  });

  const managerOptions = [
    { value: '', label: '(none)' },
    ...managers.map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName}` })),
  ];
  const customer = customers.find((c) => c.id === contract.customerId);
  const customerLabel = customer ? `${customer.number} — ${customer.name}` : contract.customerId;

  const knownById = new Map(candidates.map((u) => [u.id, u]));
  for (const id of contract.consultantIds) {
    if (!knownById.has(id)) knownById.set(id, { id, firstName: id, lastName: '', email: '', roles: [] });
  }
  const consultantOptions = Array.from(knownById.values()).map((u) => ({
    value: u.id,
    label: u.firstName || u.lastName ? `${u.firstName} ${u.lastName}`.trim() : u.id,
  }));

  const activeTasks = contract.tasks.filter((t) => t.deletedAt === null);
  const archivedTasks = contract.tasks.filter((t) => t.deletedAt !== null);

  const form = useAppForm({
    defaultValues: {
      subject: contract.subject,
      customerId: contract.customerId,
      clientManagerId: contract.clientManagerId ?? '',
      start: contract.start,
      end: contract.end ?? '',
      consultantIds: [...contract.consultantIds],
      tasks: activeTasks.map((t) => ({ id: t.id as string | null, name: t.name, rate: t.rate })),
    } satisfies ContractFormValues,
    validators: { onChange: contractFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        await submitUpdateContract({ data: { id: contract.id, contract: value } });
        await router.invalidate();
        form.reset(value);
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, FIELD_PATHS);

  return (
    <main>
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">Contract #{contract.number}</h1>
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={async () => {
            if (!confirm(`Delete contract #${contract.number} — ${contract.subject}?`)) return;
            try {
              await deleteContract({ data: contract.id });
              router.navigate({ to: '/admin/contracts' });
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
        <form
          onSubmit={(event) => {
            event.preventDefault();
            event.stopPropagation();
            form.handleSubmit();
          }}
          className="mt-4 grid max-w-2xl gap-6"
        >
          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">General</h2>
            <form.AppField name="subject">{(field) => <field.TextField label="Subject" />}</form.AppField>
            <div className="grid gap-2">
              <Label htmlFor="customer">Customer</Label>
              <Input id="customer" value={customerLabel} disabled />
            </div>
            <form.AppField name="clientManagerId">
              {(field) => (
                <field.ComboboxField
                  label="Client manager"
                  options={managerOptions}
                  placeholder="(none)"
                  emptyMessage="No client managers found."
                />
              )}
            </form.AppField>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Period</h2>
            <div className="grid grid-cols-2 gap-4">
              <form.AppField name="start">{(field) => <field.DateField label="Start" />}</form.AppField>
              <form.AppField name="end">{(field) => <field.DateField label="End (optional)" />}</form.AppField>
            </div>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Consultants</h2>
            <form.AppField name="consultantIds">
              {(field) => (
                <field.MultiSelectField
                  label="Assigned consultants"
                  options={consultantOptions}
                  placeholder="Select consultants"
                  emptyMessage="No users found."
                />
              )}
            </form.AppField>
          </section>

          <ContractTaskSubform form={form as never} archived={archivedTasks} />

          <form.FormActions cancel />
        </form>
      </form.AppForm>
    </main>
  );
}
