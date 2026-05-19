import { useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { COUNTRY_OPTIONS } from '#/lib/countries';
import { UserRole } from '#/api/users';
import { customerFormSchema, type CustomerFormValues } from '#/features/customers/schemas';
import { submitCreateCustomer } from '#/features/customers/server-fns';
import { fetchUsersByRole } from '#/features/users/server-fns';

const FIELD_PATHS: (keyof CustomerFormValues)[] = [
  'name',
  'contactName',
  'contactEmail',
  'street',
  'zip',
  'city',
  'country',
  'clientManagerId',
];

export function CustomerCreateForm() {
  const router = useRouter();

  const { data: managers = [] } = useQuery({
    queryKey: ['client-managers'],
    queryFn: () => fetchUsersByRole({ data: UserRole.ClientManager }),
  });
  const managerOptions = [
    { value: '', label: '(none)' },
    ...managers.map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName}` })),
  ];

  const form = useAppForm({
    defaultValues: {
      name: '',
      contactName: '',
      contactEmail: '',
      street: '',
      zip: '',
      city: '',
      country: '',
      clientManagerId: '',
    } satisfies CustomerFormValues,
    validators: { onChange: customerFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        const created = await submitCreateCustomer({ data: value });
        await router.invalidate();
        if (created) router.navigate({ to: '/admin/customers/$id', params: { id: created.id } });
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, FIELD_PATHS);

  return (
    <main>
      <h1 className="text-2xl font-bold">New customer</h1>
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
            <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Contact</h2>
            <form.AppField name="contactName">{(field) => <field.TextField label="Contact name" />}</form.AppField>
            <form.AppField name="contactEmail">
              {(field) => <field.TextField label="Contact email" type="email" />}
            </form.AppField>
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
            <h2 className="text-lg font-semibold">Address</h2>
            <form.AppField name="street">{(field) => <field.TextField label="Street" />}</form.AppField>
            <div className="grid grid-cols-2 gap-4">
              <form.AppField name="zip">{(field) => <field.TextField label="Zip" />}</form.AppField>
              <form.AppField name="city">{(field) => <field.TextField label="City" />}</form.AppField>
            </div>
            <form.AppField name="country">
              {(field) => <field.ComboboxField label="Country" options={COUNTRY_OPTIONS} />}
            </form.AppField>
          </section>

          <form.FormActions saveLabel="Create customer" savePendingLabel="Creating…" />
        </form>
      </form.AppForm>
    </main>
  );
}
