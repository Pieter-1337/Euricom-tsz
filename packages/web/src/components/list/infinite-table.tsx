import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type SortingState,
} from '@tanstack/react-table';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table.tsx';

export interface InfiniteTableProps<TItem> {
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

export function InfiniteTable<TItem>(props: InfiniteTableProps<TItem>) {
  const table = useReactTable({
    data: props.items,
    columns: props.columns,
    state: { sorting: props.sorting },
    onSortingChange: props.onSortingChange,
    manualSorting: true,
    enableMultiSort: false,
    getRowId: props.rowKey,
    getCoreRowModel: getCoreRowModel(),
  });

  const colSpan = props.columns.length || 1;

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
        {props.isLoading && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              Loading…
            </TableCell>
          </TableRow>
        )}
        {!props.isLoading && props.error != null && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-destructive">
              Failed to load data.
            </TableCell>
          </TableRow>
        )}
        {!props.isLoading && props.error == null && props.items.length === 0 && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              {props.emptyState}
            </TableCell>
          </TableRow>
        )}
        {table.getRowModel().rows.map((row) => (
          <TableRow
            key={row.id}
            onClick={props.onRowClick ? () => props.onRowClick!(row.original) : undefined}
            className={props.onRowClick ? 'cursor-pointer' : undefined}
          >
            {row.getVisibleCells().map((cell) => (
              <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
            ))}
          </TableRow>
        ))}
        {props.isFetchingNextPage && (
          <TableRow>
            <TableCell colSpan={colSpan} className="text-center text-muted-foreground">
              Loading more…
            </TableCell>
          </TableRow>
        )}
        <TableRow
          ref={props.sentinelRef as (node: HTMLTableRowElement | null) => void}
          className="h-0 border-0"
          aria-hidden
        />
      </TableBody>
    </Table>
  );
}
