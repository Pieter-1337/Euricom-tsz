import { HeadContent, Link, Outlet, Scripts, createRootRoute } from '@tanstack/react-router';
import { useEffect } from 'react';

import appCss from '../styles.css?url';
import { ErrorBoundary } from '#/components/error-boundary';
import { ThemeToggle } from '#/components/theme-toggle';
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
    console.log('redirect check => [RootLayout] session:', session);
    if (!session?.user) {
      console.log('No session found, redirecting to Microsoft sign-in...');
      authClient.signIn.social({ provider: 'microsoft', callbackURL: '/' });
    } else {
      console.log('Session found for user:', session.user);
    }
  }, []);

  if (!session?.user) return null;

  return (
    <div className="mx-auto max-w-3xl p-6">
      <nav className="mb-6 flex items-center gap-4 text-sm">
        <Link to="/" className="[&.active]:font-bold">
          Home
        </Link>
        <Link to="/animals" className="[&.active]:font-bold">
          Animals
        </Link>
        <div className="ml-auto flex items-center gap-3">
          <span className="text-muted-foreground">
            {session.user.name} ·{' '}
            <button
              className="underline-offset-4 hover:underline"
              onClick={() => authClient.signOut({ fetchOptions: { onSuccess: () => window.location.assign('/') } })}
            >
              Sign out
            </button>
          </span>
          <ThemeToggle />
        </div>
      </nav>
      <Outlet />
    </div>
  );
}
