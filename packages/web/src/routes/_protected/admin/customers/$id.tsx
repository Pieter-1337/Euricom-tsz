import { createFileRoute } from '@tanstack/react-router';
import { fetchCustomer } from '#/features/customers/server-fns';
import { CustomerEditCard } from '#/features/customers/components/customer-edit-card';

export const Route = createFileRoute('/_protected/admin/customers/$id')({
  loader: ({ params }) => fetchCustomer({ data: params.id }),
  component: EditCustomerPage,
});

function EditCustomerPage() {
  const customer = Route.useLoaderData();

  if (!customer) {
    return (
      <main>
        <h1 className="text-2xl font-bold">Customer not found</h1>
      </main>
    );
  }

  return (
    <main>
      <CustomerEditCard customer={customer} />
    </main>
  );
}
