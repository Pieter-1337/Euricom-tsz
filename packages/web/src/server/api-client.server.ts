import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from '#/api/schema';
import { ApiRequestError } from '#/api/client';
import { env } from '#/env.server';
import { auth } from './auth.server';
import { getRequest, getCookie } from '@tanstack/react-start/server';
import { getVerifiedImpersonationTargetId } from './impersonation.server';

const bearerMiddleware: Middleware = {
  async onRequest({ request }) {
    const incoming = getRequest();
    const token = await auth.api.getAccessToken({
      headers: incoming.headers,
      body: { providerId: 'microsoft' },
    });
    if (token?.accessToken) {
      request.headers.set('Authorization', `Bearer ${token.accessToken}`);
    }

    // Emit X-Impersonate-User only when the cookie is signed and session-bound.
    try {
      const session = await auth.api.getSession({ headers: incoming.headers });
      if (session?.user?.id) {
        const cookieValue = getCookie('__Host-tsz_impersonate');
        const targetId = getVerifiedImpersonationTargetId(cookieValue, session.user.id);
        if (targetId) {
          request.headers.set('X-Impersonate-User', targetId);
        }
      }
    } catch {
      // If we cannot verify the cookie, simply omit the header.
    }

    return request;
  },
};

const errorMiddleware: Middleware = {
  async onResponse({ response }) {
    if (!response.ok) {
      const contentType = response.headers.get('content-type') ?? '';
      let problem: import('#/api/client').ProblemDetails | null = null;
      if (contentType.includes('application/problem+json') || contentType.includes('application/json')) {
        try {
          problem = (await response.clone().json()) as import('#/api/client').ProblemDetails;
        } catch {
          problem = null;
        }
      }
      throw new ApiRequestError(response.status, problem);
    }
  },
};

export const apiClient = createClient<paths>({ baseUrl: env.API_URL });
apiClient.use(bearerMiddleware);
apiClient.use(errorMiddleware);
