import type { ReactNode } from 'react';
import {
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableRow,
} from '#/components/ui/table.tsx';

interface InfiniteTableProps<TItem> {
  items: TItem[];
  rowKey: (item: TItem) => string;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: unknown;
  emptyState: string;
  sentinelRef: (node: HTMLElement | null) => void;
  onRowClick?: (item: TItem) => void;
  children: {
    head: ReactNode;
    row: (item: TItem) => ReactNode;
  };
}

export function InfiniteTable<TItem>({
  items,
  rowKey,
  isLoading,
  isFetchingNextPage,
  error,
  emptyState,
  sentinelRef,
  onRowClick,
  children,
}: InfiniteTableProps<TItem>) {
  const colSpan = 99;

  return (
    <Table>
      <TableHeader>
        <TableRow>{children.head}</TableRow>
      </TableHeader>
      <TableBody>
        {isLoading && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              Loading…
            </TableCell>
          </TableRow>
        )}
        {!isLoading && error != null && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-destructive">
              Failed to load data.
            </TableCell>
          </TableRow>
        )}
        {!isLoading && error == null && items.length === 0 && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              {emptyState}
            </TableCell>
          </TableRow>
        )}
        {items.map((item) => (
          <TableRow
            key={rowKey(item)}
            onClick={onRowClick ? () => onRowClick(item) : undefined}
            className={onRowClick ? 'cursor-pointer' : undefined}
          >
            {children.row(item)}
          </TableRow>
        ))}
        {isFetchingNextPage && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              Loading more…
            </TableCell>
          </TableRow>
        )}
        <TableRow
          ref={sentinelRef as (node: HTMLTableRowElement | null) => void}
          className="h-0 border-0"
          aria-hidden
        />
      </TableBody>
    </Table>
  );
}
