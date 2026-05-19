import type { ReactNode } from 'react';
import { ListToolbar, type ListToolbarProps } from '#/components/list/list-toolbar.tsx';
import { InfiniteTable, type InfiniteTableProps } from '#/components/list/infinite-table.tsx';

interface ListShellProps<TItem> {
  toolbar: ListToolbarProps;
  table: InfiniteTableProps<TItem>;
  actions?: ReactNode;
}

export function ListShell<TItem>(props: ListShellProps<TItem>) {
  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <ListToolbar {...props.toolbar} />
        {props.actions && <div className="flex items-center gap-2">{props.actions}</div>}
      </div>
      <InfiniteTable {...props.table} />
    </div>
  );
}
