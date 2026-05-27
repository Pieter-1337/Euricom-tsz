import { createFileRoute, Link, Outlet, redirect } from '@tanstack/react-router';
import {
  Building2,
  CalendarDays,
  CalendarRange,
  ChevronLeft,
  ChevronRight,
  Clock,
  FileText,
  User as UserIcon,
  Users as UsersIcon,
} from 'lucide-react';
import { useEffect, useState, type ComponentType, type SVGProps } from 'react';

import { UserRole } from '#/api/users';
import { ThemeToggle } from '#/components/theme-toggle';
import { Button } from '#/components/ui/button';
import { Separator } from '#/components/ui/separator';
import { Tooltip, TooltipProvider } from '#/components/ui/tooltip';
import { authClient } from '#/lib/auth-client';
import { cn } from '#/lib/utils';
import type { SessionUser } from '#/server/auth-functions';
import { getCurrentUser } from '#/server/current-user';
import { getImpersonation, stopImpersonation, type ImpersonationInfo } from '#/server/impersonation.server';
import { ImpersonateDialog } from '#/features/users/components/impersonate-dialog';

export const Route = createFileRoute('/_protected')({
  beforeLoad: async ({ context }) => {
    const session = (context as any).session as { user: SessionUser } | null;
    if (!session) return;
    const [currentUser, impersonation] = await Promise.all([getCurrentUser(), getImpersonation()]);
    if (!currentUser) throw redirect({ to: '/no-access' });
    return { user: session.user, currentUser, impersonation };
  },
  component: ProtectedLayout,
});

const SIDEBAR_KEY = 'tsz.sidebar.collapsed';

function ProtectedLayout() {
  const { user, currentUser, impersonation } = Route.useRouteContext() as {
    user: SessionUser;
    currentUser: NonNullable<Awaited<ReturnType<typeof getCurrentUser>>>;
    impersonation: ImpersonationInfo | null;
  };
  const isAdmin = currentUser.roles.includes(UserRole.Admin);
  const isClientManager = currentUser.roles.includes(UserRole.ClientManager);
  const canManageClients = isAdmin || isClientManager;

  // Real identity for the header greeting: split on first space
  const realFirstName = user.name?.split(' ')[0] ?? user.name;

  const [collapsed, setCollapsed] = useState<boolean>(false);
  const [impersonateOpen, setImpersonateOpen] = useState(false);

  useEffect(() => {
    setCollapsed(localStorage.getItem(SIDEBAR_KEY) === '1');
  }, []);

  useEffect(() => {
    localStorage.setItem(SIDEBAR_KEY, collapsed ? '1' : '0');
  }, [collapsed]);

  const handleStop = async () => {
    try {
      await stopImpersonation();
    } finally {
      window.location.assign('/');
    }
  };

  return (
    <TooltipProvider delayDuration={100} skipDelayDuration={200}>
      <div className="flex min-h-screen flex-col">
        <header className="print:hidden bg-euri-charcoal flex h-14 flex-shrink-0 items-center justify-between border-b border-white/[0.06] px-5 text-white">
          <div className="flex items-center gap-3">
            <Brandmark className="h-[22px] w-[22px]" />
            <span className="text-sm font-semibold tracking-[0.01em]">Timesheet Zone</span>
          </div>

          <div className="flex items-center gap-1">
            <div className="flex items-center gap-2 rounded-full px-3 py-1.5 text-[13px] text-white/85">
              <UserIcon className="stroke-euri-green h-3.5 w-3.5" strokeWidth={1.75} />
              <span>
                Hi, <strong className="font-semibold text-white">{realFirstName}</strong>
              </span>
            </div>
            {isAdmin && !impersonation && (
              <>
                <Separator orientation="vertical" className="mx-1.5 !h-4 bg-white/10" />
                <Button
                  variant="ghost"
                  size="sm"
                  className="focus-visible:outline-euri-green h-8 px-2.5 text-[12.5px] font-normal text-white/65 hover:bg-white/[0.04] hover:text-white focus-visible:ring-0 focus-visible:outline-2 focus-visible:outline-offset-2"
                  onClick={() => setImpersonateOpen(true)}
                >
                  Impersonate
                </Button>
              </>
            )}
            <Separator orientation="vertical" className="mx-1.5 !h-4 bg-white/10" />
            <Button
              variant="ghost"
              size="sm"
              className="focus-visible:outline-euri-green h-8 px-2.5 text-[12.5px] font-normal text-white/65 hover:bg-white/[0.04] hover:text-white focus-visible:ring-0 focus-visible:outline-2 focus-visible:outline-offset-2"
              onClick={async () => {
                try {
                  await stopImpersonation();
                } catch {
                  // Best-effort cookie clear; never block sign-out on it.
                }
                authClient.signOut({
                  fetchOptions: { onSuccess: () => window.location.assign('/') },
                });
              }}
            >
              Sign out
            </Button>
            <ThemeToggle />
          </div>
        </header>

        {impersonation && (
          <div className="print:hidden flex items-center justify-between bg-destructive px-5 py-2 text-sm text-destructive-foreground">
            <span>
              You are impersonating <strong className="font-semibold">{impersonation.targetName}</strong>
            </span>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 px-2.5 text-[12.5px] font-semibold text-destructive-foreground hover:bg-white/20 hover:text-destructive-foreground focus-visible:ring-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              onClick={() => void handleStop()}
            >
              Stop
            </Button>
          </div>
        )}

        <div className="relative flex min-h-0 flex-1">
          <Sidebar
            collapsed={collapsed}
            onToggle={() => setCollapsed((c) => !c)}
            isAdmin={isAdmin}
            canManageClients={canManageClients}
          />
          <Main />
        </div>
      </div>

      <ImpersonateDialog open={impersonateOpen} onOpenChange={setImpersonateOpen} />
    </TooltipProvider>
  );
}

type IconComponent = ComponentType<SVGProps<SVGSVGElement>>;

function Sidebar({
  collapsed,
  onToggle,
  isAdmin,
  canManageClients,
}: {
  collapsed: boolean;
  onToggle: () => void;
  isAdmin: boolean;
  canManageClients: boolean;
}) {
  return (
    <aside
      aria-label="Primary navigation"
      style={{ width: collapsed ? 64 : 240 }}
      className={cn(
        'print:hidden flex flex-shrink-0 flex-col overflow-hidden',
        'bg-euri-steel-light border-r border-black/[0.06]',
        'dark:bg-euri-sidebar-dark dark:border-white/[0.06]',
        '[transition:width_220ms_cubic-bezier(.22,.61,.36,1)]',
        'motion-reduce:transition-none',
      )}
    >
      <nav
        className={cn(
          'flex min-h-0 flex-1 flex-col overflow-hidden',
          collapsed ? 'gap-1 py-4' : 'gap-0 px-3 pt-[22px] pb-3',
        )}
      >
        <div className={cn('flex flex-col', collapsed ? 'items-center gap-1' : 'gap-0.5')}>
          <NavLink to="/timesheets" icon={CalendarDays} label="Timesheets" collapsed={collapsed} />
          <NavLink to="/time-entry" icon={Clock} label="Time Entry" collapsed={collapsed} />
          <NavLink to="/leaves" icon={CalendarRange} label="Leave overview" collapsed={collapsed} />
          {isAdmin && <NavLink to="/admin/users" icon={UsersIcon} label="Users" collapsed={collapsed} />}
          {canManageClients && <NavLink to="/customers" icon={Building2} label="Customers" collapsed={collapsed} />}
          {canManageClients && <NavLink to="/contracts" icon={FileText} label="Contracts" collapsed={collapsed} />}
        </div>
      </nav>

      <div
        className={cn(
          'border-t border-black/[0.08] dark:border-white/[0.07]',
          collapsed ? 'flex justify-center px-0 py-2.5' : 'flex justify-end px-3 py-2.5',
        )}
      >
        <Tooltip content={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} side="right">
          <Button
            variant="outline"
            size="sm"
            onClick={onToggle}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            aria-expanded={!collapsed}
            className={cn(
              'h-8 gap-2 rounded-lg text-[12px] font-medium shadow-none',
              'hover:text-euri-charcoal border-black/10 bg-transparent text-[#3A4651] hover:bg-black/[0.04]',
              'dark:border-white/10 dark:bg-transparent dark:text-white/70 dark:hover:bg-white/[0.04] dark:hover:text-white',
              'focus-visible:outline-euri-green focus-visible:ring-0 focus-visible:outline-2 focus-visible:outline-offset-2',
              collapsed ? 'w-10 px-0' : 'px-2.5',
            )}
          >
            {collapsed ? (
              <ChevronRight className="size-3.5" strokeWidth={1.75} />
            ) : (
              <>
                <ChevronLeft className="size-3.5" strokeWidth={1.75} />
                <span>Collapse</span>
              </>
            )}
          </Button>
        </Tooltip>
      </div>
    </aside>
  );
}

function NavLink({
  to,
  icon: Icon,
  label,
  collapsed,
  exact,
}: {
  to: string;
  icon: IconComponent;
  label: string;
  collapsed: boolean;
  exact?: boolean;
}) {
  if (collapsed) {
    return (
      <Tooltip content={label} side="right">
        <Link
          to={to}
          activeOptions={exact ? { exact: true } : undefined}
          className={cn(
            'group relative grid h-10 w-10 place-items-center rounded-[10px]',
            'hover:text-euri-charcoal text-[#3A4651] hover:bg-black/[0.04]',
            'dark:text-white/70 dark:hover:bg-white/[0.04] dark:hover:text-white',
            'transition-colors duration-[120ms]',
            'focus-visible:outline-euri-green focus-visible:outline-2 focus-visible:outline-offset-2',
            '[&.active]:bg-euri-green/10 [&.active]:text-euri-charcoal',
            'dark:[&.active]:bg-euri-green/[0.08] dark:[&.active]:text-white',
          )}
        >
          <span
            aria-hidden="true"
            className="bg-euri-green group-[.active]:block pointer-events-none absolute top-2 -left-3 bottom-2 hidden w-0.5 rounded-sm"
          />
          <Icon className="group-[.active]:stroke-euri-green h-[18px] w-[18px] stroke-current" strokeWidth={1.75} />
        </Link>
      </Tooltip>
    );
  }
  return (
    <Link
      to={to}
      activeOptions={exact ? { exact: true } : undefined}
      className={cn(
        'group relative flex items-center gap-3 rounded-lg px-3 py-[9px] text-[13.5px] font-medium',
        'hover:text-euri-charcoal text-[#3A4651] hover:bg-black/[0.04]',
        'dark:text-white/70 dark:hover:bg-white/[0.04] dark:hover:text-white',
        'transition-colors duration-[120ms]',
        'focus-visible:outline-euri-green focus-visible:outline-2 focus-visible:outline-offset-2',
        '[&.active]:bg-euri-green/10 [&.active]:text-euri-charcoal',
        'dark:[&.active]:bg-euri-green/[0.08] dark:[&.active]:text-white',
        '[&.active]:shadow-[inset_2px_0_0_var(--euri-green)]',
      )}
    >
      <Icon className="group-[.active]:stroke-euri-green h-4 w-4 stroke-current" strokeWidth={1.75} />
      <span>{label}</span>
    </Link>
  );
}

function Main() {
  return (
    <main className="dark:bg-euri-charcoal relative flex-1 overflow-auto bg-white">
      <div
        aria-hidden="true"
        className={cn(
          'pointer-events-none absolute inset-0',
          'opacity-[0.18] [filter:invert(1)]',
          'dark:opacity-[0.55] dark:[filter:none]',
          "[background-image:url('/grid-pattern.svg')] [background-size:160px_160px]",
        )}
      />
      <div className="text-euri-charcoal relative p-6 dark:text-white">
        <Outlet />
      </div>
    </main>
  );
}

function Brandmark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 256 256" className={cn('fill-euri-green', className)} aria-label="Euricom" role="img">
      <path d="M39.981 39.9826H109.998V0H35.1838C15.7526 0 0 15.7532 0 35.1852V110.003H39.9675V39.9826H39.981Z" />
      <path d="M156.702 128.008C156.702 143.856 143.86 156.711 128 156.711C112.14 156.711 99.2979 143.869 99.2979 128.008C99.2979 112.147 112.153 99.3047 128 99.3047C143.847 99.3047 156.702 112.147 156.702 128.008Z" />
      <path d="M220.809 0H145.994V39.9691H216.011V109.989H255.979V35.1852C255.979 15.7532 240.226 0 220.795 0" />
      <path d="M216.019 145.998V216.018H146.002V255.987H220.816C240.248 255.987 256 240.234 256 220.802V145.984H216.033L216.019 145.998Z" />
      <path d="M39.9832 216.016V145.996H0.015625V220.814C0.015625 240.246 15.7681 255.999 35.1994 255.999H110.014V216.03H39.9966L39.9832 216.016Z" />
    </svg>
  );
}
