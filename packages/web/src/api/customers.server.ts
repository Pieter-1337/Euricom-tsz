import { apiClient as client } from '#/server/api-client.server';
import { ApiRequestError } from '#/api/client';
import type { Customer, CreateCustomerRequest, UpdateCustomerRequest } from '#/api/customers';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';

type CustomerSortKey = 'number' | 'name' | 'city' | 'contactEmail';

export const getCustomersPaged = async (params: KeysetQueryParams<CustomerSortKey>): Promise<KeysetPage<Customer>> => {
  const resp = await client.GET('/api/customers/paged', {
    params: {
      query: {
        search: params.search,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        pageSize: params.pageSize,
        cursor: params.cursor,
        deletedOnly: params.deletedOnly,
      },
    },
  });
  return resp.data ?? { items: [], nextCursor: null, total: 0 };
};

export const getCustomers = async (): Promise<Customer[]> => {
  const resp = await client.GET('/api/customers');
  return resp.data ?? [];
};

export const getCustomerById = async (id: string): Promise<Customer | null> => {
  try {
    const resp = await client.GET('/api/customers/{id}', { params: { path: { id } } });
    return resp.data ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const createCustomer = async (body: CreateCustomerRequest): Promise<Customer> => {
  const resp = await client.POST('/api/customers', { body });
  return resp.data!;
};

export const updateCustomer = async (id: string, body: UpdateCustomerRequest): Promise<Customer> => {
  const resp = await client.PUT('/api/customers/{id}', { params: { path: { id } }, body });
  return resp.data!;
};

export const removeCustomer = async (id: string): Promise<void> => {
  await client.DELETE('/api/customers/{id}', { params: { path: { id } } });
};
