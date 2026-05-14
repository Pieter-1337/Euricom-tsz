import type { ReactNode } from 'react';
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
  items: TItem[];
  rowKey: (item: TItem) => string;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: unknown;
  emptyState: string;
  sentinelRef: (node: HTMLElement | null) => void;
  onRowClick?: (item: TItem) => void;
  actions?: ReactNode;
  children: {
    head: ReactNode;
    row: (item: TItem) => ReactNode;
  };
}

export function ListShell<TItem>({
  search,
  onSearchChange,
  total,
  toggle,
  items,
  rowKey,
  isLoading,
  isFetchingNextPage,
  error,
  emptyState,
  sentinelRef,
  onRowClick,
  actions,
  children,
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
        items={items}
        rowKey={rowKey}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState={emptyState}
        sentinelRef={sentinelRef}
        onRowClick={onRowClick}
      >
        {children}
      </InfiniteTable>
    </div>
  );
}
