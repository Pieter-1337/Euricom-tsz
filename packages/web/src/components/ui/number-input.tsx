import { useRef, useState } from 'react';
import { ChevronDownIcon, ChevronUpIcon } from 'lucide-react';

import { Input } from '#/components/ui/input';
import { cn } from '#/lib/utils.ts';

type NumberInputProps = {
  id?: string;
  name?: string;
  value: number;
  onChange: (value: number) => void;
  onBlur?: () => void;
  min?: number;
  max?: number;
  step?: number;
  className?: string;
  'aria-invalid'?: boolean | undefined;
};

function NumberInput({
  id,
  name,
  value,
  onChange,
  onBlur,
  min,
  max,
  step = 1,
  className,
  'aria-invalid': ariaInvalid,
}: NumberInputProps) {
  const [text, setText] = useState<string>(() => String(value));
  const lastValueRef = useRef(value);

  // Resync display when the external value changes (reset, external update).
  if (value !== lastValueRef.current) {
    setText(String(value));
    lastValueRef.current = value;
  }

  const fallback = min ?? 0;

  const stepBy = (delta: number) => {
    const current = Number(text);
    const base = Number.isFinite(current) ? current : fallback;
    let next = base + delta;
    if (min !== undefined && next < min) next = min;
    if (max !== undefined && next > max) next = max;
    setText(String(next));
    lastValueRef.current = next;
    onChange(next);
  };

  const current = Number(text);
  const isAtMin = min !== undefined && Number.isFinite(current) && current <= min;
  const isAtMax = max !== undefined && Number.isFinite(current) && current >= max;

  return (
    <div className={cn('relative', className)}>
      <Input
        id={id}
        name={name}
        type="number"
        min={min}
        max={max}
        step={step}
        value={text}
        aria-invalid={ariaInvalid}
        className="pr-9 [appearance:textfield] [&::-webkit-inner-spin-button]:m-0 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:m-0 [&::-webkit-outer-spin-button]:appearance-none"
        onBlur={() => {
          if (text === '' || Number.isNaN(Number(text))) {
            setText(String(value));
          }
          onBlur?.();
        }}
        onChange={(e) => {
          const raw = e.target.value;
          setText(raw);
          const n = raw === '' ? fallback : Number(raw);
          if (!Number.isNaN(n)) {
            lastValueRef.current = n;
            onChange(n);
          }
        }}
      />
      <div className="absolute inset-y-0 right-0 flex flex-col border-l border-input">
        <button
          type="button"
          tabIndex={-1}
          disabled={isAtMax}
          aria-label="Increase"
          onClick={() => stepBy(step)}
          className="flex flex-1 items-center justify-center px-1.5 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground disabled:cursor-not-allowed disabled:opacity-40"
        >
          <ChevronUpIcon className="size-3" />
        </button>
        <div className="h-px bg-input" />
        <button
          type="button"
          tabIndex={-1}
          disabled={isAtMin}
          aria-label="Decrease"
          onClick={() => stepBy(-step)}
          className="flex flex-1 items-center justify-center px-1.5 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground disabled:cursor-not-allowed disabled:opacity-40"
        >
          <ChevronDownIcon className="size-3" />
        </button>
      </div>
    </div>
  );
}

export { NumberInput };
