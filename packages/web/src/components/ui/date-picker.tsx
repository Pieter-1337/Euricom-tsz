'use client';

import * as React from 'react';
import { format, isValid, parse } from 'date-fns';
import { CalendarIcon } from 'lucide-react';

import { Button } from '#/components/ui/button';
import { Calendar } from '#/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '#/components/ui/popover';
import { cn } from '#/lib/utils.ts';

const ISO = 'yyyy-MM-dd';
const DISPLAY = 'dd/MM/yyyy';

const isoToDate = (iso: string): Date | undefined => {
  if (!iso) return undefined;
  const d = parse(iso, ISO, new Date());
  return isValid(d) ? d : undefined;
};

const dateToIso = (d: Date): string => format(d, ISO);

type DatePickerProps = {
  id?: string;
  name?: string;
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  'aria-invalid'?: boolean | undefined;
};

function DatePicker({
  id,
  name,
  value,
  onChange,
  onBlur,
  placeholder = DISPLAY.toLowerCase(),
  className,
  disabled,
  'aria-invalid': ariaInvalid,
}: DatePickerProps) {
  const [open, setOpen] = React.useState(false);
  const selected = isoToDate(value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          aria-invalid={ariaInvalid}
          disabled={disabled}
          onBlur={onBlur}
          className={cn('h-9 w-full justify-between px-3 font-normal', !selected && 'text-muted-foreground', className)}
        >
          <span>{selected ? format(selected, DISPLAY) : placeholder}</span>
          <CalendarIcon className="size-4 text-muted-foreground opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="single"
          selected={selected}
          defaultMonth={selected}
          onSelect={(d) => {
            if (d) {
              onChange(dateToIso(d));
              setOpen(false);
            }
          }}
          autoFocus
        />
      </PopoverContent>
      {name && <input type="hidden" name={name} value={value} />}
    </Popover>
  );
}

export { DatePicker };
