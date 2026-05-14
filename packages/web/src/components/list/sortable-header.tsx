import { ChevronDown, ChevronUp } from 'lucide-react';
import { Button } from '#/components/ui/button.tsx';
import type { SortDir } from '#/api/pagination.ts';

interface SortableHeaderProps<TSortKey extends string> {
  label: string;
  sortKey: TSortKey;
  currentSortBy: TSortKey;
  currentSortDir: SortDir;
  onSort: (key: TSortKey) => void;
}

export function SortableHeader<TSortKey extends string>({
  label,
  sortKey,
  currentSortBy,
  currentSortDir,
  onSort,
}: SortableHeaderProps<TSortKey>) {
  const isActive = sortKey === currentSortBy;

  return (
    <Button
      variant="ghost"
      size="sm"
      className="cursor-pointer -ml-3 h-8 font-medium"
      onClick={() => onSort(sortKey)}
    >
      {label}
      {isActive && (
        <span className="ml-1">
          {currentSortDir === 'asc' ? (
            <ChevronUp className="size-3.5" />
          ) : (
            <ChevronDown className="size-3.5" />
          )}
        </span>
      )}
    </Button>
  );
}
