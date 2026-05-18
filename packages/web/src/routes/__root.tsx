import { HeadContent, Outlet, Scripts, createRootRoute } from '@tanstack/react-router';
import { useEffect } from 'react';

import appCss from '../styles.css?url';
import { ErrorBoundary } from '#/components/error-boundary';
import { getSession } from '#/server/auth-functions';
import { authClient } from '#/lib/auth-client';

const themeInitScript = `(() => {
  try {
    const stored = localStorage.getItem('theme');
    if (stored !== 'light') {
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
    links: [
      { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
      { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossOrigin: 'anonymous' },
      {
        rel: 'stylesheet',
        href: 'https://fonts.googleapis.com/css2?family=Montserrat:ital,wght@0,300;0,400;0,500;0,600;0,700;1,300;1,400&display=swap',
      },
      { rel: 'stylesheet', href: appCss },
    ],
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
    <html lang="en" className="dark" suppressHydrationWarning>
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
