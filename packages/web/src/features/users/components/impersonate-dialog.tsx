import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';

import { UserRole, type User } from '#/api/users';
import { Badge } from '#/components/ui/badge';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '#/components/ui/command';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '#/components/ui/dialog';
import { fetchImpersonationTargets } from '#/features/users/server-fns';
import { startImpersonation } from '#/server/impersonation';

interface ImpersonateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ImpersonateDialog({ open, onOpenChange }: ImpersonateDialogProps) {
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 250);
    return () => clearTimeout(timer);
  }, [search]);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['impersonation-targets', debouncedSearch],
    queryFn: () =>
      fetchImpersonationTargets({
        data: {
          search: debouncedSearch || undefined,
          sortBy: 'name',
          sortDir: 'asc',
          pageSize: 50,
        },
      }),
    enabled: open,
    // Always refetch when the dialog (re)opens so role changes / newly-eligible users show immediately.
    staleTime: 0,
    refetchOnMount: 'always',
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
      <DialogContent className="p-0 sm:max-w-2xl" showCloseButton={true}>
        <DialogHeader className="sr-only">
          <DialogTitle>Impersonate user</DialogTitle>
          <DialogDescription>Search and select a user to impersonate.</DialogDescription>
        </DialogHeader>
        <Command shouldFilter={false}>
          <CommandInput placeholder="Search users..." value={search} onValueChange={setSearch} autoFocus />
          <CommandList className="max-h-[60vh] min-h-[250px]">
            {isLoading && (
              <div className="flex items-center justify-center py-6 text-sm text-muted-foreground">
                <Loader2 className="mr-2 h-4 w-4 animate-spin" strokeWidth={1.75} />
                Loading users...
              </div>
            )}
            {!isLoading && isError && (
              <div className="py-6 text-center text-sm text-destructive">Failed to load users. Please try again.</div>
            )}
            {!isLoading && !isError && targets.length === 0 && <CommandEmpty>No users found.</CommandEmpty>}
            {!isLoading && !isError && targets.length > 0 && (
              <CommandGroup>
                {targets.map((user) => (
                  <CommandItem
                    key={user.id}
                    value={user.id}
                    onSelect={() => void handleSelect(user)}
                    className="cursor-pointer"
                  >
                    <span className="font-medium">
                      {user.firstName} {user.lastName}
                    </span>
                    <span className="ml-2 text-muted-foreground text-xs">{user.email}</span>
                    <span className="ml-auto flex flex-wrap gap-1">
                      {user.roles
                        .filter((r) => r !== UserRole.User)
                        .map((r) => (
                          <Badge key={r} variant="secondary">
                            {r}
                          </Badge>
                        ))}
                    </span>
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
