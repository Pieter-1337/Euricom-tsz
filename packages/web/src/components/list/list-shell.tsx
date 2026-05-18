import type { ReactNode } from 'react';
import type { ColumnDef, OnChangeFn, SortingState } from '@tanstack/react-table';
import { ListToolbar } from '#/components/list/list-toolbar.tsx';
import { InfiniteTable } from '#/components/list/infinite-table.tsx';

interface ListShellToggle {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

interface ListShellProps<TItem> {
  search: string;
  onSearchChange: (value: string) => void;
  total: number | undefined;
  toggle?: ListShellToggle;
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
  actions?: ReactNode;
}

export function ListShell<TItem>({
  search,
  onSearchChange,
  total,
  toggle,
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
  actions,
}: ListShellProps<TItem>) {
  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <ListToolbar
          search={search}
          onSearchChange={onSearchChange}
          total={total}
          toggle={toggle}
        />
        {actions && <div className="flex items-center gap-2">{actions}</div>}
      </div>
      <InfiniteTable
        columns={columns}
        items={items}
        rowKey={rowKey}
        sorting={sorting}
        onSortingChange={onSortingChange}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState={emptyState}
        sentinelRef={sentinelRef}
        onRowClick={onRowClick}
      />
    </div>
  );
}
