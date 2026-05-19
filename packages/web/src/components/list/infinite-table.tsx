import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type SortingState,
} from '@tanstack/react-table';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table.tsx';

interface InfiniteTableProps<TItem> {
  columns: ColumnDef<TItem, unknown>[];
  items: TItem[];
  rowKey: (item: TItem) => string;
  sorting: SortingState;
  onSortingChange: OnChangeFn<SortingState>;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: unknown;
  emptyState: string;
  sentinelRef: (node: HTMLElement | null) => void;
  onRowClick?: (item: TItem) => void;
}

export function InfiniteTable<TItem>({
  columns,
  items,
  rowKey,
  sorting,
  onSortingChange,
  isLoading,
  isFetchingNextPage,
  error,
  emptyState,
  sentinelRef,
  onRowClick,
}: InfiniteTableProps<TItem>) {
  const table = useReactTable({
    data: items,
    columns,
    state: { sorting },
    onSortingChange,
    manualSorting: true,
    enableMultiSort: false,
    getRowId: rowKey,
    getCoreRowModel: getCoreRowModel(),
  });

  const colSpan = columns.length || 1;

  return (
    <Table>
      <TableHeader>
        {table.getHeaderGroups().map((headerGroup) => (
          <TableRow key={headerGroup.id}>
            {headerGroup.headers.map((header) => (
              <TableHead key={header.id}>
                {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
              </TableHead>
            ))}
          </TableRow>
        ))}
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
        {table.getRowModel().rows.map((row) => (
          <TableRow
            key={row.id}
            onClick={onRowClick ? () => onRowClick(row.original) : undefined}
            className={onRowClick ? 'cursor-pointer' : undefined}
          >
            {row.getVisibleCells().map((cell) => (
              <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
            ))}
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
