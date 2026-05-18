import { ChevronDown, ChevronUp } from 'lucide-react';
import type { Column } from '@tanstack/react-table';
import { Button } from '#/components/ui/button.tsx';

export function SortableHeaderCell<TItem>({ column, label }: { column: Column<TItem, unknown>; label: string }) {
  const sorted = column.getIsSorted();
  return (
    <Button
      variant="ghost"
      size="sm"
      className="cursor-pointer -ml-3 h-8 font-medium"
      onClick={() => column.toggleSorting(sorted === 'asc')}
    >
      {label}
      <span className="ml-1 inline-flex" aria-hidden={!sorted}>
        {sorted === 'asc' && <ChevronUp className="size-3.5" />}
        {sorted === 'desc' && <ChevronDown className="size-3.5" />}
        {!sorted && <ChevronUp className="size-3.5 invisible" />}
      </span>
    </Button>
  );
}
