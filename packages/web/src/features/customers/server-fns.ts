import { createServerFn } from '@tanstack/react-start';
import type { Customer } from '#/api/customers';
import { getCustomers } from '#/api/customers.server';

export const fetchCustomers = createServerFn({ method: 'GET' }).handler(
  async (): Promise<Customer[]> => getCustomers(),
);
