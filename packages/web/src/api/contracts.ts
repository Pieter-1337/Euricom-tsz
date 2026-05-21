import { type components } from './schema';

export type Contract = components['schemas']['ContractDto'];
export type ContractSummary = components['schemas']['ContractSummaryDto'];
export type ContractTask = components['schemas']['ContractTaskDto'];
export type CreateContractRequest = components['schemas']['CreateContractCommand'];
export type UpdateContractRequest = components['schemas']['UpdateContractCommand'];
export type UpdateContractTask = components['schemas']['UpdateContractTaskDto'];

export type ContractSortKey = 'number' | 'subject' | 'start';

export type ContractsListExtraParams = {
  activeOnDate?: string;
  customerId?: string;
};
