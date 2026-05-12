import { betterAuth } from 'better-auth';
import { tanstackStartCookies } from 'better-auth/tanstack-start';
import Database from 'better-sqlite3';

console.log('[auth init] MICROSOFT_CLIENT_ID:', process.env.MICROSOFT_CLIENT_ID?.slice(0, 8) + '...');
console.log('[auth init] MICROSOFT_CLIENT_SECRET length:', process.env.MICROSOFT_CLIENT_SECRET?.length ?? 'UNDEFINED');
console.log('[auth init] BETTER_AUTH_URL:', process.env.BETTER_AUTH_URL);

const sqlite = new Database('auth.db');
sqlite.exec('PRAGMA journal_mode = WAL');

export const auth = betterAuth({
  database: sqlite,
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
      scope: ['openid', 'profile', 'email', 'offline_access', `api://${process.env.API_CLIENT_ID}/access`],
      prompt: 'select_account',
      mapProfileToUser: (profile) => ({
        id: profile.oid ?? profile.sub,
        email: profile.email ?? profile.preferred_username,
        name: profile.name,
      }),
      disableDefaultScope: true,
    },
  },
  advanced: {
    // __Host- prefix: locks cookies to exact origin, blocks subdomain injection (incl. OAuth state hijack)
    // secure + httpOnly: HTTPS-only transmission, no JS access — applies to all cookies incl. state/PKCE
    //cookiePrefix: '__Host-timesheetzone',
    defaultCookieAttributes: {
      secure: true,
      httpOnly: true,
      path: '/',
    },
    // SameSite stays on BA default (Lax) for session_token: the post-OAuth redirect chain
    // (microsoft.com → /callback → /) keeps its origin as microsoft.com for the whole chain
    // per the SameSite spec, so a Strict cookie is dropped on the first / request and the
    // route guard sees a null session. Strict would only work if the IdP shared our eTLD+1.
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
