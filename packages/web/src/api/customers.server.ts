import { apiClient as client } from '#/server/api-client.server';
import type { Customer } from '#/api/customers';

export const getCustomers = async (): Promise<Customer[]> => {
  const resp = await client.GET('/api/customers');
  return resp.data ?? [];
};
