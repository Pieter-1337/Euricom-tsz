import createClient, { type Middleware } from 'openapi-fetch';
import { type paths } from './schema';

export class ApiRequestError extends Error {
  constructor(public status: number) {
    super(`HTTP ${status}`);
  }
}

export const errorMiddleware: Middleware = {
  onResponse({ response }) {
    if (!response.ok) throw new ApiRequestError(response.status);
  },
};

export const client = createClient<paths>({ baseUrl: process.env.SERVER_URL });
client.use(errorMiddleware);
