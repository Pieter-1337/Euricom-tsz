import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from '#/api/schema';
import { ApiRequestError } from '#/api/client';
import { auth } from './auth';
import { getRequest } from '@tanstack/react-start/server';

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
    return request;
  },
};

const errorMiddleware: Middleware = {
  onResponse({ response }) {
    if (!response.ok) throw new ApiRequestError(response.status);
  },
};

export const apiClient = createClient<paths>({ baseUrl: process.env.API_URL });
apiClient.use(bearerMiddleware);
apiClient.use(errorMiddleware);
