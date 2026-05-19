'use client';

import * as React from 'react';
import { CheckIcon, ChevronsUpDownIcon, XIcon } from 'lucide-react';

import { cn } from '#/lib/utils.ts';
import { Badge } from '#/components/ui/badge.tsx';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover.tsx';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '#/components/ui/command.tsx';

export type MultiComboboxOption = { value: string; label: string };

type MultiComboboxProps = Omit<React.ComponentProps<'button'>, 'value' | 'onChange'> & {
  value: readonly string[];
  onValueChange: (value: string[]) => void;
  options: readonly MultiComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyMessage?: string;
};

function MultiCombobox({
  value,
  onValueChange,
  options,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  emptyMessage = 'No results.',
  className,
  disabled,
  ...triggerProps
}: MultiComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const selected = options.filter((o) => value.includes(o.value));

  const toggle = (v: string) => {
    if (value.includes(v)) onValueChange(value.filter((x) => x !== v));
    else onValueChange([...value, v]);
  };

  const remove = (v: string, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onValueChange(value.filter((x) => x !== v));
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          data-slot="multi-combobox-trigger"
          className={cn(
            'flex min-h-9 w-full items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 py-1.5 text-sm shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:bg-input/30 dark:hover:bg-input/50',
            selected.length === 0 && 'text-muted-foreground',
            className,
          )}
          {...triggerProps}
        >
          {selected.length === 0 ? (
            <span className="line-clamp-1">{placeholder}</span>
          ) : (
            <div className="flex flex-1 flex-wrap items-center gap-1">
              {selected.map((o) => (
                <Badge key={o.value} variant="secondary" className="gap-1 pr-1">
                  {o.label}
                  <span
                    role="button"
                    tabIndex={-1}
                    aria-label={`Remove ${o.label}`}
                    onClick={(e) => remove(o.value, e)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        e.stopPropagation();
                        onValueChange(value.filter((x) => x !== o.value));
                      }
                    }}
                    className="rounded-sm hover:bg-muted-foreground/20 focus:outline-none"
                  >
                    <XIcon className="size-3" />
                  </span>
                </Badge>
              ))}
            </div>
          )}
          <ChevronsUpDownIcon className="size-4 shrink-0 opacity-50" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-[var(--radix-popover-trigger-width)] p-0"
        onOpenAutoFocus={(e) => {
          e.preventDefault();
        }}
      >
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            <CommandEmpty>{emptyMessage}</CommandEmpty>
            <CommandGroup>
              {options.map((o) => {
                const isSelected = value.includes(o.value);
                return (
                  <CommandItem key={o.value} value={o.label} onSelect={() => toggle(o.value)}>
                    <CheckIcon className={cn('size-4', isSelected ? 'opacity-100' : 'opacity-0')} />
                    {o.label}
                  </CommandItem>
                );
              })}
            </CommandGroup>
            {value.length > 0 && (
              <>
                <CommandSeparator />
                <CommandGroup>
                  <CommandItem
                    onSelect={() => {
                      onValueChange([]);
                      setOpen(false);
                    }}
                    className="justify-center text-center"
                  >
                    Clear
                  </CommandItem>
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

export { MultiCombobox };
