import { createFileRoute, Link, Outlet, redirect } from '@tanstack/react-router';
import { Home as HomeIcon, Users as UsersIcon, User as UserIcon } from 'lucide-react';
import type { ReactNode } from 'react';
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
  const { currentUser } = Route.useRouteContext() as {
    user: SessionUser;
    currentUser: NonNullable<Awaited<ReturnType<typeof getCurrentUser>>>;
  };
  const isAdmin = currentUser.role === UserRole.Admin;
  const firstName = currentUser.firstName;

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-12 items-center justify-between bg-slate-900 px-6 text-slate-100">
        <span className="text-base font-semibold text-emerald-400">Timesheet Zone</span>
        <div className="flex items-center gap-3 text-sm">
          <UserIcon className="h-4 w-4" />
          <span>Hi, {firstName}!</span>
          <button
            className="text-slate-300 underline-offset-4 hover:text-slate-100 hover:underline"
            onClick={() =>
              authClient.signOut({ fetchOptions: { onSuccess: () => window.location.assign('/') } })
            }
          >
            Sign out
          </button>
          <ThemeToggle />
        </div>
      </header>
      <div className="flex flex-1">
        <aside className="w-64 shrink-0 border-r bg-sidebar text-sidebar-foreground">
          <nav className="px-3 py-6">
            <SidebarLink
              to="/"
              icon={<HomeIcon className="h-4 w-4" />}
              label="Home"
              activeOptions={{ exact: true }}
            />
            {isAdmin && (
              <>
                <div className="my-3 border-t border-sidebar-border" />
                <SidebarLink to="/admin/users" icon={<UsersIcon className="h-4 w-4" />} label="Users" />
              </>
            )}
          </nav>
        </aside>
        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function SidebarLink({
  to,
  icon,
  label,
  activeOptions,
}: {
  to: string;
  icon: ReactNode;
  label: string;
  activeOptions?: { exact?: boolean };
}) {
  return (
    <Link
      to={to}
      className="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground [&.active]:bg-sidebar-accent [&.active]:font-medium [&.active]:text-sidebar-accent-foreground"
      activeOptions={activeOptions}
    >
      {icon}
      {label}
    </Link>
  );
}
