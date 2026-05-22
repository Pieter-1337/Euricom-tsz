import { useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { contractFormSchema, type ContractFormValues, type ContractTaskFormValue } from '#/features/contracts/schemas';
import { submitCreateContract } from '#/features/contracts/server-fns';
import { fetchAllCustomers } from '#/features/customers/server-fns';

const FIELD_PATHS: (keyof ContractFormValues)[] = ['subject', 'customerId', 'start', 'end'];

export function ContractCreateForm() {
  const router = useRouter();

  const { data: customers = [] } = useQuery({
    queryKey: ['customers', 'all'],
    queryFn: () => fetchAllCustomers(),
  });

  const customerOptions = customers.map((c) => ({ value: c.id, label: `${c.number} — ${c.name}` }));

  const form = useAppForm({
    defaultValues: {
      subject: '',
      customerId: '',
      clientManagerId: '',
      start: '',
      end: '',
      consultantIds: [] as string[],
      tasks: [] as ContractTaskFormValue[],
    } satisfies ContractFormValues,
    validators: { onChange: contractFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        await submitCreateContract({ data: value });
        await router.invalidate();
        router.navigate({ to: '/admin/contracts' });
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, FIELD_PATHS);

  return (
    <main>
      <h1 className="text-2xl font-bold">New contract</h1>
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
            <form.AppField name="customerId">
              {(field) => (
                <field.ComboboxField
                  label="Customer"
                  options={customerOptions}
                  placeholder="Select customer"
                  emptyMessage="No customers found."
                />
              )}
            </form.AppField>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Period</h2>
            <div className="grid grid-cols-2 gap-4">
              <form.AppField name="start">{(field) => <field.DateField label="Start" />}</form.AppField>
              <form.AppField name="end">{(field) => <field.DateField label="End" />}</form.AppField>
            </div>
          </section>

          <form.FormActions saveLabel="Create contract" savePendingLabel="Creating…" />
        </form>
      </form.AppForm>
    </main>
  );
}
