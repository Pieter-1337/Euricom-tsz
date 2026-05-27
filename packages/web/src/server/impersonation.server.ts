import { createServerFn } from '@tanstack/react-start';
import { getCookie, setCookie, deleteCookie } from '@tanstack/react-start/server';
import { createHmac } from 'node:crypto';
import { z } from 'zod';
import { env } from '#/env.server';
import { auth } from './auth.server';
import { getRequest } from '@tanstack/react-start/server';

const COOKIE_NAME = '__Host-tsz_impersonate';

const payloadSchema = z.object({
  impersonatorId: z.string(),
  targetId: z.string().uuid(),
  targetName: z.string(),
});

type ImpersonationPayload = z.infer<typeof payloadSchema>;

function sign(payload: ImpersonationPayload): string {
  const body = JSON.stringify(payload);
  const hmac = createHmac('sha256', env.BETTER_AUTH_SECRET).update(body).digest('base64url');
  return `${Buffer.from(body).toString('base64url')}.${hmac}`;
}

function verify(value: string): ImpersonationPayload | null {
  try {
    const dotIdx = value.lastIndexOf('.');
    if (dotIdx === -1) return null;
    const bodyB64 = value.slice(0, dotIdx);
    const sig = value.slice(dotIdx + 1);
    const body = Buffer.from(bodyB64, 'base64url').toString('utf8');
    const expected = createHmac('sha256', env.BETTER_AUTH_SECRET).update(body).digest('base64url');
    if (sig !== expected) return null;
    return payloadSchema.parse(JSON.parse(body));
  } catch {
    return null;
  }
}

async function getCurrentSessionUserId(): Promise<string | null> {
  try {
    const request = getRequest();
    const session = await auth.api.getSession({ headers: request.headers });
    return session?.user?.id ?? null;
  } catch {
    return null;
  }
}

export type ImpersonationInfo = {
  targetId: string;
  targetName: string;
};

export const startImpersonation = createServerFn({ method: 'POST' })
  .inputValidator(z.object({ targetUserId: z.string().uuid(), targetName: z.string() }))
  .handler(async ({ data }): Promise<void> => {
    const sessionUserId = await getCurrentSessionUserId();
    if (!sessionUserId) throw new Error('Not authenticated');

    const payload: ImpersonationPayload = {
      impersonatorId: sessionUserId,
      targetId: data.targetUserId,
      targetName: data.targetName,
    };

    setCookie(COOKIE_NAME, sign(payload), {
      httpOnly: true,
      secure: true,
      sameSite: 'strict',
      path: '/',
    });
  });

export const stopImpersonation = createServerFn({ method: 'POST' }).handler(async (): Promise<void> => {
  deleteCookie(COOKIE_NAME, { path: '/' });
});

/// Returns the active impersonation target, or null when absent/invalid/stale.
export const getImpersonation = createServerFn({ method: 'GET' }).handler(
  async (): Promise<ImpersonationInfo | null> => {
    const raw = getCookie(COOKIE_NAME);
    if (!raw) return null;
    const payload = verify(raw);
    if (!payload) return null;
    const sessionUserId = await getCurrentSessionUserId();
    if (!sessionUserId || payload.impersonatorId !== sessionUserId) return null;
    return { targetId: payload.targetId, targetName: payload.targetName };
  },
);

/// Returns the verified target id for use in bearerMiddleware; no server fn overhead.
export function getVerifiedImpersonationTargetId(
  cookieValue: string | undefined,
  sessionUserId: string,
): string | null {
  if (!cookieValue) return null;
  const payload = verify(cookieValue);
  if (!payload) return null;
  if (payload.impersonatorId !== sessionUserId) return null;
  return payload.targetId;
}
