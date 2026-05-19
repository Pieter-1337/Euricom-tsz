import { createServerFn } from '@tanstack/react-start';
import {
  createCustomer,
  getCustomerById,
  getCustomersPaged,
  removeCustomer,
  updateCustomer,
} from '#/api/customers.server';
import type { Customer, CreateCustomerRequest, UpdateCustomerRequest } from '#/api/customers';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';
import { throwApiError } from '#/lib/server-error';
import {
  customerFormSchema,
  customerIdSchema,
  getCustomersPagedParamsSchema,
  saveCustomerInputSchema,
  type CustomerFormValues,
  type CustomerSortKey,
} from '#/features/customers/schemas';

const nullIfEmpty = (s: string): string | null => {
  const t = s.trim();
  return t.length === 0 ? null : t;
};

const toCreateRequest = (form: CustomerFormValues): CreateCustomerRequest => {
  const street = nullIfEmpty(form.street);
  const zip = nullIfEmpty(form.zip);
  const city = nullIfEmpty(form.city);
  const country = nullIfEmpty(form.country);
  const anyAddress = street || zip || city || country;
  return {
    name: form.name.trim(),
    contactPerson: { name: nullIfEmpty(form.contactName), email: form.contactEmail.trim() },
    address: anyAddress ? { street, zip, city, country } : null,
    clientManagerId: nullIfEmpty(form.clientManagerId),
  };
};

const toUpdateRequest = (id: string, form: CustomerFormValues): UpdateCustomerRequest => ({
  id,
  ...toCreateRequest(form),
});

export const fetchCustomersPaged = createServerFn({ method: 'GET' })
  .inputValidator((input: unknown) => getCustomersPagedParamsSchema.parse(input))
  .handler(
    async ({ data }): Promise<KeysetPage<Customer>> => getCustomersPaged(data as KeysetQueryParams<CustomerSortKey>),
  );

export const fetchCustomer = createServerFn({ method: 'GET' })
  .inputValidator(customerIdSchema)
  .handler(async ({ data: id }) => getCustomerById(id));

export const submitCreateCustomer = createServerFn({ method: 'POST' })
  .inputValidator(customerFormSchema)
  .handler(async ({ data }) => {
    try {
      return await createCustomer(toCreateRequest(data));
    } catch (e) {
      throwApiError(e);
    }
  });

export const saveCustomer = createServerFn({ method: 'POST' })
  .inputValidator(saveCustomerInputSchema)
  .handler(async ({ data }) => {
    try {
      await updateCustomer(data.id, toUpdateRequest(data.id, data.customer));
    } catch (e) {
      throwApiError(e);
    }
  });

export const deleteCustomer = createServerFn({ method: 'POST' })
  .inputValidator(customerIdSchema)
  .handler(async ({ data: id }) => {
    try {
      await removeCustomer(id);
    } catch (e) {
      throwApiError(e);
    }
  });
