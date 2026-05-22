import { useRef, useState } from 'react';
import { Input } from '#/components/ui/input';
import { cn } from '#/lib/utils.ts';

type DecimalInputProps = {
  id?: string;
  name?: string;
  value: number;
  onChange: (value: number) => void;
  onBlur?: () => void;
  min?: number;
  max?: number;
  decimals?: number;
  className?: string;
  disabled?: boolean;
  'aria-invalid'?: boolean | undefined;
};

function formatNumber(value: number, decimals: number): string {
  if (!Number.isFinite(value)) return '';
  return value.toFixed(decimals).replace('.', ',');
}

function parseDecimal(raw: string): number {
  const cleaned = raw.replace(',', '.').trim();
  if (cleaned === '' || cleaned === '-' || cleaned === '.') return NaN;
  return Number(cleaned);
}

function DecimalInput({
  id,
  name,
  value,
  onChange,
  onBlur,
  min,
  max,
  decimals = 2,
  className,
  disabled,
  'aria-invalid': ariaInvalid,
}: DecimalInputProps) {
  const [text, setText] = useState<string>(() => formatNumber(value, decimals));
  const lastValueRef = useRef(value);

  if (value !== lastValueRef.current) {
    setText(formatNumber(value, decimals));
    lastValueRef.current = value;
  }

  return (
    <Input
      id={id}
      name={name}
      type="text"
      inputMode="decimal"
      value={text}
      disabled={disabled}
      aria-invalid={ariaInvalid}
      className={cn(className)}
      onBlur={() => {
        const parsed = parseDecimal(text);
        if (Number.isNaN(parsed)) {
          setText(formatNumber(value, decimals));
        } else {
          let clamped = parsed;
          if (min !== undefined && clamped < min) clamped = min;
          if (max !== undefined && clamped > max) clamped = max;
          setText(formatNumber(clamped, decimals));
          lastValueRef.current = clamped;
          if (clamped !== value) onChange(clamped);
        }
        onBlur?.();
      }}
      onChange={(e) => {
        const raw = e.target.value.replace(/[^0-9.,-]/g, '');
        setText(raw);
        const n = parseDecimal(raw);
        if (!Number.isNaN(n)) {
          lastValueRef.current = n;
          onChange(n);
        }
      }}
    />
  );
}

export { DecimalInput };
