import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import {
  setImpersonationCookie,
  clearImpersonationCookie,
  readImpersonation,
  type ImpersonationInfo,
} from './impersonation-cookie.server';

export type { ImpersonationInfo };

export const startImpersonation = createServerFn({ method: 'POST' })
  .inputValidator(z.object({ targetUserId: z.string().uuid(), targetName: z.string() }))
  .handler(async ({ data }): Promise<void> => {
    await setImpersonationCookie(data.targetUserId, data.targetName);
  });

export const stopImpersonation = createServerFn({ method: 'POST' }).handler(async (): Promise<void> => {
  clearImpersonationCookie();
});

export const getImpersonation = createServerFn({ method: 'GET' }).handler(
  async (): Promise<ImpersonationInfo | null> => readImpersonation(),
);
