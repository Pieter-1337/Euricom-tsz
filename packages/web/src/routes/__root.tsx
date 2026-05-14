import { HeadContent, Outlet, Scripts, createRootRoute } from '@tanstack/react-router';
import { useEffect } from 'react';

import appCss from '../styles.css?url';
import { ErrorBoundary } from '#/components/error-boundary';
import { getSession } from '#/lib/auth.functions';
import { authClient } from '#/lib/auth-client';

const themeInitScript = `(() => {
  try {
    const stored = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (stored === 'dark' || (!stored && prefersDark)) {
      document.documentElement.classList.add('dark');
    }
  } catch {}
})();`;

export const Route = createRootRoute({
  head: () => ({
    meta: [
      { charSet: 'utf-8' },
      { name: 'viewport', content: 'width=device-width, initial-scale=1' },
      { title: 'TanStack Start' },
    ],
    links: [{ rel: 'stylesheet', href: appCss }],
  }),
  beforeLoad: async () => {
    const session = await getSession();
    return { session };
  },
  component: RootLayout,
  shellComponent: RootDocument,
  errorComponent: ErrorBoundary,
  notFoundComponent: () => (
    <main>
      <h1 className="text-2xl font-bold">Page not found</h1>
      <p className="mt-2 text-gray-600">The page you're looking for doesn't exist.</p>
    </main>
  ),
});

function RootDocument({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <HeadContent />
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body>
        {children}
        <Scripts />
      </body>
    </html>
  );
}

function RootLayout() {
  const { session } = Route.useRouteContext();

  useEffect(() => {
    if (!session?.user) {
      authClient.signIn.social({ provider: 'microsoft', callbackURL: '/' });
    }
  }, []);

  if (!session?.user) return null;

  return <Outlet />;
}
