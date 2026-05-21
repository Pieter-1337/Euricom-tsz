import { apiClient as client } from '#/server/api-client.server';
import { ApiRequestError } from '#/api/client';
import type {
  Contract,
  ContractSortKey,
  ContractSummary,
  ContractsListExtraParams,
  CreateContractRequest,
  UpdateContractRequest,
} from '#/api/contracts';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';

export const getContractsPaged = async (
  params: KeysetQueryParams<ContractSortKey> & ContractsListExtraParams,
): Promise<KeysetPage<ContractSummary>> => {
  const resp = await client.GET('/api/contracts', {
    params: {
      query: {
        search: params.search,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        pageSize: params.pageSize,
        cursor: params.cursor,
        deletedOnly: params.deletedOnly,
        activeOnDate: params.activeOnDate,
        customerId: params.customerId,
      },
    },
  });
  return resp.data ?? { items: [], nextCursor: null, total: 0 };
};

export const createContract = async (body: CreateContractRequest): Promise<Contract> => {
  const resp = await client.POST('/api/contracts', { body });
  return resp.data!;
};

export const getContractById = async (id: string): Promise<Contract | null> => {
  try {
    const resp = await client.GET('/api/contracts/{id}', { params: { path: { id } } });
    return resp.data ?? null;
  } catch (e) {
    if (e instanceof ApiRequestError && e.status === 404) return null;
    throw e;
  }
};

export const updateContract = async (id: string, body: UpdateContractRequest): Promise<Contract> => {
  const resp = await client.PUT('/api/contracts/{id}', { params: { path: { id } }, body });
  return resp.data!;
};

export const removeContract = async (id: string): Promise<void> => {
  await client.DELETE('/api/contracts/{id}', { params: { path: { id } } });
};
