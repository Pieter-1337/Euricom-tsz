import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { cn } from '#/lib/utils';

interface AddRowPopoverProps<T> {
  triggerLabel: string;
  items: T[];
  getKey: (item: T) => string;
  renderItem: (item: T) => React.ReactNode;
  onAdd: (item: T) => void;
  emptyMessage: string;
  contentWidth?: string;
}

export function AddRowPopover<T>({
  triggerLabel,
  items,
  getKey,
  renderItem,
  onAdd,
  emptyMessage,
  contentWidth = 'w-72',
}: AddRowPopoverProps<T>) {
  const [open, setOpen] = useState(false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="sm" className="gap-2 text-[13px]">
          <Plus className="h-4 w-4" />
          {triggerLabel}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className={cn(contentWidth, 'p-0')}>
        {items.length === 0 ? (
          <div className="px-4 py-3 text-sm text-[#6B7682] dark:text-white/40">{emptyMessage}</div>
        ) : (
          <ul>
            {items.map((item) => (
              <li key={getKey(item)}>
                <button
                  className="w-full cursor-pointer px-4 py-2.5 text-left text-sm hover:bg-black/[0.04] dark:hover:bg-white/[0.04] transition-colors duration-[120ms]"
                  onClick={() => {
                    onAdd(item);
                    setOpen(false);
                  }}
                >
                  {renderItem(item)}
                </button>
              </li>
            ))}
          </ul>
        )}
      </PopoverContent>
    </Popover>
  );
}
