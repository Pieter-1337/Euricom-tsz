'use client';

import * as React from 'react';
import { CheckIcon, ChevronsUpDownIcon } from 'lucide-react';

import { cn } from '#/lib/utils.ts';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover.tsx';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '#/components/ui/command.tsx';

export type ComboboxOption = { value: string; label: string };

type ComboboxProps = Omit<React.ComponentProps<'button'>, 'value' | 'onChange'> & {
  value: string;
  onValueChange: (value: string) => void;
  options: readonly ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyMessage?: string;
  /** Show a "Clear selection" item at the top of the dropdown when a value is selected. */
  allowClear?: boolean;
  /** Label for the clear item; only used when allowClear is true. */
  clearLabel?: string;
};

function Combobox({
  value,
  onValueChange,
  options,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  emptyMessage = 'No results.',
  allowClear = false,
  clearLabel = 'Clear selection',
  className,
  disabled,
  ...triggerProps
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const selected = options.find((o) => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          data-slot="combobox-trigger"
          className={cn(
            'flex h-9 w-full items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 py-2 text-sm whitespace-nowrap shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:bg-input/30 dark:hover:bg-input/50',
            !selected && 'text-muted-foreground',
            className,
          )}
          {...triggerProps}
        >
          <span className="line-clamp-1">{selected?.label ?? placeholder}</span>
          <ChevronsUpDownIcon className="size-4 shrink-0 opacity-50" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-[var(--radix-popover-trigger-width)] p-0"
        onOpenAutoFocus={(e) => {
          // Let CommandInput grab focus instead of the list.
          e.preventDefault();
        }}
      >
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandEmpty>{emptyMessage}</CommandEmpty>
            <CommandGroup>
              {allowClear && value !== '' && (
                <CommandItem
                  value={clearLabel}
                  onSelect={() => {
                    onValueChange('');
                    setOpen(false);
                  }}
                  className="text-muted-foreground"
                >
                  <CheckIcon className="size-4 opacity-0" />
                  {clearLabel}
                </CommandItem>
              )}
              {options.map((o) => (
                <CommandItem
                  key={o.value}
                  value={o.label}
                  onSelect={() => {
                    onValueChange(o.value);
                    setOpen(false);
                  }}
                >
                  <CheckIcon className={cn('size-4', o.value === value ? 'opacity-100' : 'opacity-0')} />
                  {o.label}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

export { Combobox };
