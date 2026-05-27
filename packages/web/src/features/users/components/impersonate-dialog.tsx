import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';

import type { User } from '#/api/users';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '#/components/ui/command';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '#/components/ui/dialog';
import { fetchImpersonationTargets } from '#/features/users/server-fns';
import { startImpersonation } from '#/server/impersonation.server';

interface ImpersonateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ImpersonateDialog({ open, onOpenChange }: ImpersonateDialogProps) {
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['impersonation-targets', search],
    queryFn: () =>
      fetchImpersonationTargets({
        data: {
          search: search || undefined,
          sortBy: 'name',
          sortDir: 'asc',
          pageSize: 50,
        },
      }),
    enabled: open,
    staleTime: 30_000,
  });

  const targets = data?.items ?? [];

  const handleSelect = async (user: User) => {
    onOpenChange(false);
    await startImpersonation({
      data: {
        targetUserId: user.id,
        targetName: `${user.firstName} ${user.lastName}`,
      },
    });
    window.location.assign('/');
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="p-0 sm:max-w-md" showCloseButton={false}>
        <DialogHeader className="sr-only">
          <DialogTitle>Impersonate user</DialogTitle>
          <DialogDescription>Search and select a user to impersonate.</DialogDescription>
        </DialogHeader>
        <Command shouldFilter={false}>
          <CommandInput placeholder="Search users..." value={search} onValueChange={setSearch} autoFocus />
          <CommandList>
            {isLoading && (
              <div className="flex items-center justify-center py-6 text-sm text-muted-foreground">
                <Loader2 className="mr-2 h-4 w-4 animate-spin" strokeWidth={1.75} />
                Loading users...
              </div>
            )}
            {!isLoading && targets.length === 0 && <CommandEmpty>No users found.</CommandEmpty>}
            {!isLoading && targets.length > 0 && (
              <CommandGroup>
                {targets.map((user) => (
                  <CommandItem key={user.id} value={user.id} onSelect={() => void handleSelect(user)}>
                    <span className="font-medium">
                      {user.firstName} {user.lastName}
                    </span>
                    <span className="ml-2 text-muted-foreground text-xs">{user.email}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </DialogContent>
    </Dialog>
  );
}
