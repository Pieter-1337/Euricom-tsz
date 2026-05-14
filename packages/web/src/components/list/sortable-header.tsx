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
      <span className="ml-1 inline-flex" aria-hidden={!isActive}>
        {isActive && currentSortDir === 'asc' && <ChevronUp className="size-3.5" />}
        {isActive && currentSortDir === 'desc' && <ChevronDown className="size-3.5" />}
        {!isActive && <ChevronUp className="size-3.5 invisible" />}
      </span>
    </Button>
  );
}
