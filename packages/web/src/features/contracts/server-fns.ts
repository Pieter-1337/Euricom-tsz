import { createServerFn } from '@tanstack/react-start';
import {
  createContract,
  getContractById,
  getContractsPaged,
  removeContract,
  updateContract,
} from '#/api/contracts.server';
import type {
  ContractSummary,
  ContractsListExtraParams,
  CreateContractRequest,
  UpdateContractRequest,
} from '#/api/contracts';
import type { KeysetPage, KeysetQueryParams } from '#/api/pagination';
import { throwApiError } from '#/lib/server-error';
import {
  contractFormSchema,
  contractIdSchema,
  getContractsPagedParamsSchema,
  saveContractInputSchema,
  type ContractFormValues,
  type ContractSortKey,
} from '#/features/contracts/schemas';

const toCreateRequest = (form: ContractFormValues): CreateContractRequest => ({
  subject: form.subject.trim(),
  customerId: form.customerId,
  start: form.start,
  end: form.end === '' ? null : form.end,
});

const toUpdateRequest = (id: string, form: ContractFormValues): UpdateContractRequest => ({
  id,
  subject: form.subject.trim(),
  clientManagerId: form.clientManagerId === '' ? null : form.clientManagerId,
  start: form.start,
  end: form.end === '' ? null : form.end,
  consultantIds: form.consultantIds,
  tasks: form.tasks.map(({ originalArchivedId: _drop, ...t }) => ({ id: t.id, name: t.name.trim(), rate: t.rate })),
});

export const fetchContractsPaged = createServerFn({ method: 'GET' })
  .inputValidator((input: unknown) => getContractsPagedParamsSchema.parse(input))
  .handler(
    async ({ data }): Promise<KeysetPage<ContractSummary>> =>
      getContractsPaged(data as KeysetQueryParams<ContractSortKey> & ContractsListExtraParams),
  );

export const fetchContract = createServerFn({ method: 'GET' })
  .inputValidator(contractIdSchema)
  .handler(async ({ data: id }) => getContractById(id));

export const submitCreateContract = createServerFn({ method: 'POST' })
  .inputValidator(contractFormSchema)
  .handler(async ({ data }) => {
    try {
      return await createContract(toCreateRequest(data));
    } catch (e) {
      throwApiError(e);
    }
  });

export const submitUpdateContract = createServerFn({ method: 'POST' })
  .inputValidator(saveContractInputSchema)
  .handler(async ({ data }) => {
    try {
      return await updateContract(data.id, toUpdateRequest(data.id, data.contract));
    } catch (e) {
      throwApiError(e);
    }
  });

export const deleteContract = createServerFn({ method: 'POST' })
  .inputValidator(contractIdSchema)
  .handler(async ({ data: id }) => {
    try {
      await removeContract(id);
    } catch (e) {
      throwApiError(e);
    }
  });
