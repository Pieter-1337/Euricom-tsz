import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/_protected/_authenticated/time-entry')({
  component: () => <Outlet />,
});
