import { createFileRoute } from '@tanstack/react-router';
import { Button } from '#/components/ui/button';
import { authClient } from '#/lib/auth-client';

export const Route = createFileRoute('/no-access')({ component: NoAccess });

function NoAccess() {
  return (
    <main className="grid min-h-[60vh] place-items-center">
      <div className="max-w-md text-center">
        <h1 className="text-2xl font-bold">No access</h1>
        <p className="text-muted-foreground mt-2">Your account isn't set up yet. Ask an administrator to add you.</p>
        <Button
          className="mt-6"
          variant="outline"
          onClick={() => authClient.signOut({ fetchOptions: { onSuccess: () => window.location.assign('/') } })}
        >
          Sign out
        </Button>
      </div>
    </main>
  );
}
