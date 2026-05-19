import { type components } from './schema';

export type Customer = components['schemas']['CustomerDto'];
export type Address = components['schemas']['AddressDto'];
export type ContactPerson = components['schemas']['ContactPersonDto'];
export type CreateCustomerRequest = components['schemas']['CreateCustomerCommand'];
export type UpdateCustomerRequest = components['schemas']['UpdateCustomerCommand'];
