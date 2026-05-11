import { betterAuth } from 'better-auth';
import { memoryAdapter } from 'better-auth/adapters/memory';
import { tanstackStartCookies } from 'better-auth/tanstack-start';

const memoryDb: Record<string, any[]> = {
  user: [],
  session: [],
  account: [],
  verification: [],
};

console.log('[auth init] MICROSOFT_CLIENT_ID:', process.env.MICROSOFT_CLIENT_ID?.slice(0, 8) + '...');
console.log('[auth init] MICROSOFT_CLIENT_SECRET length:', process.env.MICROSOFT_CLIENT_SECRET?.length ?? 'UNDEFINED');
console.log('[auth init] BETTER_AUTH_URL:', process.env.BETTER_AUTH_URL);

export const auth = betterAuth({
  database: memoryAdapter(memoryDb),
  secret: process.env.BETTER_AUTH_SECRET!,
  baseURL: process.env.BETTER_AUTH_URL!,
  logger: { level: 'debug' },
  onAPIError: {
    onError: (error, ctx) => {
      console.error('[better-auth] onAPIError:', error);
    },
  },
  socialProviders: {
    microsoft: {
      clientId: process.env.MICROSOFT_CLIENT_ID!,
      clientSecret: process.env.MICROSOFT_CLIENT_SECRET!,
      tenantId: process.env.MICROSOFT_TENANT_ID!,
      scopes: ['openid', 'profile', 'email', 'offline_access', 'api://5f7eb51a-66af-44e0-9ada-9354dbc0c19c/access'],
      prompt: 'login',
    },
  },
  session: {
    cookieCache: {
      enabled: false,
    },
  },
  account: {
    storeAccountCookie: false,
  },
  plugins: [tanstackStartCookies()],
});
