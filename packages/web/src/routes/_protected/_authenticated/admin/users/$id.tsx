import { createFileRoute } from '@tanstack/react-router';
import { fetchUserAndLeaves } from '#/features/users/server-fns';
import { UserEditCard } from '#/features/users/components/user-edit-card';
import { LeaveOverviewSection } from '#/features/users/components/leave-overview-section';

export const Route = createFileRoute('/_protected/_authenticated/admin/users/$id')({
  loader: ({ params }) => fetchUserAndLeaves({ data: params.id }),
  component: EditUserPage,
});

function EditUserPage() {
  const { user, leaves } = Route.useLoaderData();

  if (!user) {
    return (
      <main>
        <h1 className="text-2xl font-bold">User not found</h1>
      </main>
    );
  }

  return (
    <main>
      <UserEditCard user={user} />
      <LeaveOverviewSection userId={user.id} leaves={leaves} />
    </main>
  );
}
