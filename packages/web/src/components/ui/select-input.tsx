import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '#/components/ui/select';
import { cn } from '#/lib/utils.ts';

type SelectOption<T extends string> = T | { value: T; label: string };

type SelectInputProps<T extends string> = {
  id?: string;
  name?: string;
  value: T;
  onValueChange: (value: T) => void;
  onBlur?: () => void;
  options: readonly SelectOption<T>[];
  placeholder?: string;
  className?: string;
  'aria-invalid'?: boolean | undefined;
};

function SelectInput<T extends string>({
  id,
  name,
  value,
  onValueChange,
  onBlur,
  options,
  placeholder,
  className,
  'aria-invalid': ariaInvalid,
}: SelectInputProps<T>) {
  return (
    <Select value={value} onValueChange={(v) => onValueChange(v as T)}>
      <SelectTrigger
        id={id}
        name={name}
        onBlur={onBlur}
        aria-invalid={ariaInvalid}
        className={cn('w-full', className)}
      >
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => {
          const v = typeof option === 'string' ? option : option.value;
          const label = typeof option === 'string' ? option : option.label;
          return (
            <SelectItem key={v} value={v}>
              {label}
            </SelectItem>
          );
        })}
      </SelectContent>
    </Select>
  );
}

export { SelectInput };
export type { SelectOption };
