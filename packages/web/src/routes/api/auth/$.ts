import { createFileRoute } from '@tanstack/react-router';
import { auth } from '#/server/auth.server';

export const Route = createFileRoute('/api/auth/$')({
  server: {
    handlers: {
      GET: async ({ request }) => {
        const url = new URL(request.url);
        if (url.pathname.includes('/callback/')) {
          console.log('[auth callback] path:', url.pathname);
          console.log('[auth callback] params:', Object.fromEntries(url.searchParams));
        }
        return auth.handler(request);
      },
      POST: ({ request }) => auth.handler(request),
    },
  },
});
