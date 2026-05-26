import { createFileRoute, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/_protected/_authenticated/leaves/')({
  beforeLoad: () => {
    const year = new Date().getFullYear();
    throw redirect({ to: '/leaves/$year', params: { year: String(year) } });
  },
});
