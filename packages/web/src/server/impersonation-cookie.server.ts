import { createHmac, timingSafeEqual } from 'node:crypto';
import { getCookie, setCookie, deleteCookie, getRequest } from '@tanstack/react-start/server';
import { z } from 'zod';
import { env } from '#/env.server';
import { auth } from './auth.server';

const COOKIE_NAME = '__Host-tsz_impersonate';

const payloadSchema = z.object({
  impersonatorId: z.string(),
  targetId: z.string().uuid(),
  targetName: z.string(),
});

type ImpersonationPayload = z.infer<typeof payloadSchema>;

export type ImpersonationInfo = {
  targetId: string;
  targetName: string;
};

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
    const sigBuf = Buffer.from(sig);
    const expectedBuf = Buffer.from(expected);
    if (sigBuf.length !== expectedBuf.length || !timingSafeEqual(sigBuf, expectedBuf)) return null;
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

export async function setImpersonationCookie(targetUserId: string, targetName: string): Promise<void> {
  const sessionUserId = await getCurrentSessionUserId();
  if (!sessionUserId) throw new Error('Not authenticated');

  const payload: ImpersonationPayload = {
    impersonatorId: sessionUserId,
    targetId: targetUserId,
    targetName,
  };

  setCookie(COOKIE_NAME, sign(payload), {
    httpOnly: true,
    secure: true,
    sameSite: 'strict',
    path: '/',
  });
}

export function clearImpersonationCookie(): void {
  // __Host- cookies require Secure + Path=/ on every Set-Cookie, including deletion, or the browser ignores it.
  deleteCookie(COOKIE_NAME, { path: '/', secure: true, sameSite: 'strict', httpOnly: true });
}

/// Returns the active impersonation target, or null when absent/invalid/stale.
export async function readImpersonation(): Promise<ImpersonationInfo | null> {
  const raw = getCookie(COOKIE_NAME);
  if (!raw) return null;
  const payload = verify(raw);
  if (!payload) return null;
  const sessionUserId = await getCurrentSessionUserId();
  if (!sessionUserId || payload.impersonatorId !== sessionUserId) return null;
  return { targetId: payload.targetId, targetName: payload.targetName };
}

/// Verifies the cookie value for use in bearerMiddleware; returns the target id when signed + session-bound.
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
