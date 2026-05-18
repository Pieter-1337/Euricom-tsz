import { betterAuth } from 'better-auth';
import { tanstackStartCookies } from 'better-auth/tanstack-start';
import Database from 'better-sqlite3';
import { env } from '#/env.server';

const sqlite = new Database('auth.db');
sqlite.exec('PRAGMA journal_mode = WAL');

export const auth = betterAuth({
  database: sqlite,
  secret: env.BETTER_AUTH_SECRET,
  baseURL: env.BETTER_AUTH_URL,
  logger: { level: 'debug' },
  onAPIError: {
    onError: (error) => {
      console.error('[better-auth] onAPIError:', error);
    },
  },
  socialProviders: {
    microsoft: {
      clientId: env.MICROSOFT_CLIENT_ID,
      clientSecret: env.MICROSOFT_CLIENT_SECRET,
      tenantId: env.MICROSOFT_TENANT_ID,
      scope: ['openid', 'profile', 'email', 'offline_access', `api://${env.API_CLIENT_ID}/access`],
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
