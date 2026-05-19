import { useQuery } from '@tanstack/react-query';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { fetchCustomers } from '#/features/customers/server-fns';

export function CustomersList() {
  const {
    data: customers,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['customers'],
    queryFn: () => fetchCustomers(),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Customers</h1>

      {error ? (
        <p className="text-destructive">Failed to load customers.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-[320px]">Id</TableHead>
              <TableHead>Name</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={2} className="text-muted-foreground">
                  Loading…
                </TableCell>
              </TableRow>
            )}
            {!isLoading && customers?.length === 0 && (
              <TableRow>
                <TableCell colSpan={2} className="text-muted-foreground">
                  No customers found.
                </TableCell>
              </TableRow>
            )}
            {customers?.map((c) => (
              <TableRow key={c.id}>
                <TableCell className="font-mono text-xs">{c.id}</TableCell>
                <TableCell>{c.name}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
