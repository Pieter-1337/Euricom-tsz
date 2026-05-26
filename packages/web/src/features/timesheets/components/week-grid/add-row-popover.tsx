import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { cn } from '#/lib/utils';

interface AddRowPopoverProps<T> {
  variant: 'task' | 'leave';
  triggerLabel: string;
  items: T[];
  getKey: (item: T) => string;
  renderItem: (item: T) => React.ReactNode;
  onAdd: (item: T) => void;
  emptyMessage: string;
  contentWidth?: string;
}

export function AddRowPopover<T>({
  variant,
  triggerLabel,
  items,
  getKey,
  renderItem,
  onAdd,
  emptyMessage,
  contentWidth = 'w-72',
}: AddRowPopoverProps<T>) {
  const [open, setOpen] = useState(false);
  const isLeave = variant === 'leave';

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            'gap-2 text-[13px]',
            isLeave && 'text-amber-700 hover:text-amber-800 dark:text-amber-400',
          )}
        >
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
                  className="w-full px-4 py-2.5 text-left text-sm hover:bg-black/[0.04] dark:hover:bg-white/[0.04] transition-colors duration-[120ms]"
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
