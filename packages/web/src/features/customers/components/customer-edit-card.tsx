import { useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import type { Customer } from '#/api/customers';
import type { CurrentUser } from '#/server/current-user';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { Button } from '#/components/ui/button';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { COUNTRY_OPTIONS } from '#/lib/countries';
import { UserRole } from '#/api/users';
import { customerFormSchema, type CustomerFormValues } from '#/features/customers/schemas';
import { deleteCustomer, saveCustomer } from '#/features/customers/server-fns';
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

export function CustomerEditCard({ customer, currentUser }: { customer: Customer; currentUser: CurrentUser }) {
  const router = useRouter();

  const isAdmin = currentUser.roles.includes(UserRole.Admin);
  const readOnly = !isAdmin;

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
      name: customer.name,
      contactName: customer.contactPerson.name ?? '',
      contactEmail: customer.contactPerson.email,
      street: customer.address.street ?? '',
      zip: customer.address.zip ?? '',
      city: customer.address.city ?? '',
      country: customer.address.country ?? '',
      clientManagerId: customer.clientManagerId ?? '',
    } satisfies CustomerFormValues,
    validators: { onChange: customerFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        await saveCustomer({ data: { id: customer.id, customer: value } });
        await router.invalidate();
        form.reset(value);
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, FIELD_PATHS);

  return (
    <>
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{customer.name}</h1>
        {isAdmin && (
          <Button
            type="button"
            variant="destructive"
            size="sm"
            onClick={async () => {
              if (!confirm(`Delete ${customer.name}?`)) return;
              try {
                await deleteCustomer({ data: customer.id });
                router.navigate({ to: '/customers' });
              } catch (e) {
                handleApiError(e);
              }
            }}
          >
            Delete
          </Button>
        )}
      </div>
      <form.AppForm>
        <form.FormErrorBanner message={serverError} />

        <form
          onSubmit={(event) => {
            event.preventDefault();
            event.stopPropagation();
            form.handleSubmit();
          }}
          className="mt-6 grid max-w-2xl gap-6"
        >
          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">General</h2>
            <div className="grid gap-2">
              <Label htmlFor="number">Customer number</Label>
              <Input id="number" value={customer.number} disabled />
            </div>
            <form.AppField name="name">{(field) => <field.TextField label="Name" disabled={readOnly} />}</form.AppField>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Contact</h2>
            <form.AppField name="contactName">
              {(field) => <field.TextField label="Contact name" disabled={readOnly} />}
            </form.AppField>
            <form.AppField name="contactEmail">
              {(field) => <field.TextField label="Contact email" type="email" disabled={readOnly} />}
            </form.AppField>
            <form.AppField name="clientManagerId">
              {(field) => (
                <field.ComboboxField
                  label="Client manager"
                  options={managerOptions}
                  placeholder="(none)"
                  emptyMessage="No client managers found."
                  disabled={readOnly}
                />
              )}
            </form.AppField>
          </section>

          <section className="grid gap-4">
            <h2 className="text-lg font-semibold">Address</h2>
            <form.AppField name="street">
              {(field) => <field.TextField label="Street" disabled={readOnly} />}
            </form.AppField>
            <div className="grid grid-cols-2 gap-4">
              <form.AppField name="zip">{(field) => <field.TextField label="Zip" disabled={readOnly} />}</form.AppField>
              <form.AppField name="city">
                {(field) => <field.TextField label="City" disabled={readOnly} />}
              </form.AppField>
            </div>
            <form.AppField name="country">
              {(field) => <field.ComboboxField label="Country" options={COUNTRY_OPTIONS} disabled={readOnly} />}
            </form.AppField>
          </section>

          {!readOnly && <form.FormActions cancel />}
        </form>
      </form.AppForm>
    </>
  );
}
