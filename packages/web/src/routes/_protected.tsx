import { createFileRoute, Link, Outlet, redirect } from '@tanstack/react-router';
import { UserRole } from '#/api/users';
import { ThemeToggle } from '#/components/theme-toggle';
import { authClient } from '#/lib/auth-client';
import { getCurrentUser } from '#/lib/current-user';
import type { SessionUser } from '#/lib/auth.functions';

export const Route = createFileRoute('/_protected')({
  beforeLoad: async ({ context }) => {
    const session = (context as any).session as { user: SessionUser } | null;
    if (!session) return;
    const currentUser = await getCurrentUser();
    if (!currentUser) throw redirect({ to: '/no-access' });
    return { user: session.user, currentUser };
  },
  component: ProtectedLayout,
});

function ProtectedLayout() {
  const ctx = Route.useRouteContext() as { user: SessionUser; currentUser: NonNullable<Awaited<ReturnType<typeof getCurrentUser>>> };
  const { user, currentUser } = ctx;
  const isAdmin = currentUser.role === UserRole.Admin;

  return (
    <>
      <nav className="mb-6 flex items-center gap-4 text-sm">
        <Link to="/" className="[&.active]:font-bold">
          Home
        </Link>
        <Link to="/animals" className="[&.active]:font-bold">
          Animals
        </Link>
        {isAdmin && (
          <Link to="/admin/users" className="[&.active]:font-bold">
            Users
          </Link>
        )}
        <div className="ml-auto flex items-center gap-3">
          <span className="text-muted-foreground">
            {user.name} ·{' '}
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
    </>
  );
}
