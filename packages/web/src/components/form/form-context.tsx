import { createFormHook, createFormHookContexts } from '@tanstack/react-form';
import { useRef, useState, type ReactNode } from 'react';
import { ChevronDownIcon, ChevronUpIcon } from 'lucide-react';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { Textarea } from '#/components/ui/textarea';
import { Checkbox } from '#/components/ui/checkbox';
import { Button } from '#/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '#/components/ui/select';
import { Combobox, type ComboboxOption } from '#/components/ui/combobox';
import { FieldError } from '#/components/form/field-error';
import { hasFormError } from '#/lib/form-utils';

export const { fieldContext, formContext, useFieldContext, useFormContext } = createFormHookContexts();

type FieldWithForm = {
  name: string;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: { setFieldMeta: (name: any, updater: (prev: any) => any) => void };
};

function clearServerErrorFor(field: FieldWithForm) {
  field.form.setFieldMeta(field.name, (prev) => {
    if (!prev?.errorMap?.onServer) return prev;
    const { onServer: _drop, ...rest } = prev.errorMap;
    return { ...prev, errorMap: rest };
  });
}

function TextField({ label, type }: { label: string; type?: string }) {
  const field = useFieldContext<string>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Input
        id={field.name}
        name={field.name}
        type={type}
        value={field.state.value}
        aria-invalid={field.state.meta.isTouched && field.state.meta.errors.length > 0 ? true : undefined}
        onBlur={field.handleBlur}
        onChange={(e) => {
          clearServerErrorFor(field);
          field.handleChange(e.target.value);
        }}
      />
      <FieldError field={field} />
    </div>
  );
}

function NumberField({
  label,
  suffix,
  min,
  max,
  step = 1,
}: {
  label: ReactNode;
  suffix?: string;
  min?: number;
  max?: number;
  step?: number;
}) {
  const field = useFieldContext<number>();
  const [text, setText] = useState<string>(() => String(field.state.value));
  const lastValueRef = useRef(field.state.value);

  // Resync display when the form value changes from outside (reset, external update).
  if (field.state.value !== lastValueRef.current) {
    setText(String(field.state.value));
    lastValueRef.current = field.state.value;
  }

  const fallback = min ?? 0;
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;

  const stepBy = (delta: number) => {
    const current = Number(text);
    const base = Number.isFinite(current) ? current : fallback;
    let next = base + delta;
    if (min !== undefined && next < min) next = min;
    if (max !== undefined && next > max) next = max;
    setText(String(next));
    lastValueRef.current = next;
    clearServerErrorFor(field);
    field.handleChange(next);
  };

  const current = Number(text);
  const isAtMin = min !== undefined && Number.isFinite(current) && current <= min;
  const isAtMax = max !== undefined && Number.isFinite(current) && current >= max;

  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Input
            id={field.name}
            name={field.name}
            type="number"
            min={min}
            max={max}
            step={step}
            value={text}
            aria-invalid={hasError ? true : undefined}
            className="pr-9 [appearance:textfield] [&::-webkit-inner-spin-button]:m-0 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:m-0 [&::-webkit-outer-spin-button]:appearance-none"
            onBlur={() => {
              if (text === '' || Number.isNaN(Number(text))) {
                setText(String(field.state.value));
              }
              field.handleBlur();
            }}
            onChange={(e) => {
              const raw = e.target.value;
              setText(raw);
              clearServerErrorFor(field);
              const n = raw === '' ? fallback : Number(raw);
              if (!Number.isNaN(n)) {
                lastValueRef.current = n;
                field.handleChange(n);
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
        {suffix && <span className="text-sm text-muted-foreground">{suffix}</span>}
      </div>
      <FieldError field={field} />
    </div>
  );
}

function SelectField<T extends string>({ label, options }: { label: string; options: readonly T[] }) {
  const field = useFieldContext<T>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Select
        value={field.state.value}
        onValueChange={(value) => {
          clearServerErrorFor(field);
          field.handleChange(value as T);
        }}
      >
        <SelectTrigger
          id={field.name}
          name={field.name}
          onBlur={field.handleBlur}
          aria-invalid={hasError ? true : undefined}
          className="w-full"
        >
          <SelectValue placeholder={`Select ${label.toLowerCase()}`} />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o} value={o}>
              {o}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <FieldError field={field} />
    </div>
  );
}

function SelectFieldKV({
  label,
  options,
}: {
  label: string;
  options: readonly { value: string; label: string }[];
}) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Select
        value={field.state.value}
        onValueChange={(value) => {
          clearServerErrorFor(field);
          field.handleChange(value);
        }}
      >
        <SelectTrigger
          id={field.name}
          name={field.name}
          onBlur={field.handleBlur}
          aria-invalid={hasError ? true : undefined}
          className="w-full"
        >
          <SelectValue placeholder={`Select ${label.toLowerCase()}`} />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o.value} value={o.value}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <FieldError field={field} />
    </div>
  );
}

function ComboboxField({
  label,
  options,
  placeholder,
  searchPlaceholder,
  emptyMessage,
}: {
  label: string;
  options: readonly ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyMessage?: string;
}) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Combobox
        id={field.name}
        name={field.name}
        value={field.state.value}
        onValueChange={(value) => {
          clearServerErrorFor(field);
          field.handleChange(value);
        }}
        onBlur={field.handleBlur}
        aria-invalid={hasError ? true : undefined}
        options={options}
        placeholder={placeholder ?? `Select ${label.toLowerCase()}`}
        searchPlaceholder={searchPlaceholder ?? `Search ${label.toLowerCase()}…`}
        emptyMessage={emptyMessage}
      />
      <FieldError field={field} />
    </div>
  );
}

function TextareaField({ label, rows }: { label: string; rows?: number }) {
  const field = useFieldContext<string>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Textarea
        id={field.name}
        name={field.name}
        rows={rows}
        value={field.state.value}
        aria-invalid={field.state.meta.isTouched && field.state.meta.errors.length > 0 ? true : undefined}
        onBlur={field.handleBlur}
        onChange={(e) => {
          clearServerErrorFor(field);
          field.handleChange(e.target.value);
        }}
      />
      <FieldError field={field} />
    </div>
  );
}

function CheckboxField({ label }: { label: string }) {
  const field = useFieldContext<boolean>();
  return (
    <div className="flex items-center gap-2">
      <Checkbox
        id={field.name}
        name={field.name}
        checked={field.state.value}
        onBlur={field.handleBlur}
        onCheckedChange={(checked) => {
          clearServerErrorFor(field);
          field.handleChange(checked === true ? true : false);
        }}
      />
      <Label htmlFor={field.name}>{label}</Label>
      <FieldError field={field} />
    </div>
  );
}

function DateField({ label }: { label: string }) {
  const field = useFieldContext<string>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <Input
        id={field.name}
        name={field.name}
        type="date"
        value={field.state.value}
        aria-invalid={field.state.meta.isTouched && field.state.meta.errors.length > 0 ? true : undefined}
        onBlur={field.handleBlur}
        onChange={(e) => {
          clearServerErrorFor(field);
          field.handleChange(e.target.value);
        }}
      />
      <FieldError field={field} />
    </div>
  );
}

function SubmitButton({ label, pendingLabel }: { label: string; pendingLabel: string }) {
  const form = useFormContext();
  return (
    <form.Subscribe
      selector={(state) => ({
        hasError: hasFormError(state.fieldMeta),
        isSubmitting: state.isSubmitting,
        isChanged: !state.isDefaultValue,
      })}
    >
      {({ hasError, isSubmitting, isChanged }) => (
        <Button type="submit" disabled={hasError || isSubmitting || !isChanged}>
          {isSubmitting ? pendingLabel : label}
        </Button>
      )}
    </form.Subscribe>
  );
}

function CancelButton({ label = 'Cancel' }: { label?: string }) {
  const form = useFormContext();
  return (
    <form.Subscribe selector={(state) => !state.isDefaultValue}>
      {(isChanged) => (
        <Button type="button" variant="outline" disabled={!isChanged} onClick={() => form.reset()}>
          {label}
        </Button>
      )}
    </form.Subscribe>
  );
}

function FormActions({
  saveLabel = 'Save',
  savePendingLabel = 'Saving…',
  cancelLabel = 'Cancel',
  cancel,
  className = 'mt-2 flex items-center justify-start gap-3',
}: {
  saveLabel?: string;
  savePendingLabel?: string;
  cancelLabel?: string;
  cancel?: boolean | (() => void);
  className?: string;
}) {
  const form = useFormContext();
  const showCancel = cancel !== undefined && cancel !== false;
  const customCancel = typeof cancel === 'function' ? cancel : undefined;
  return (
    <form.Subscribe
      selector={(state) => ({
        hasError: hasFormError(state.fieldMeta),
        isSubmitting: state.isSubmitting,
        isChanged: !state.isDefaultValue,
      })}
    >
      {({ hasError, isSubmitting, isChanged }) => (
        <div className={className}>
          <Button type="submit" disabled={hasError || isSubmitting || !isChanged}>
            {isSubmitting ? savePendingLabel : saveLabel}
          </Button>
          {showCancel && (customCancel || isChanged) && (
            <Button type="button" variant="outline" onClick={() => (customCancel ? customCancel() : form.reset())}>
              {cancelLabel}
            </Button>
          )}
        </div>
      )}
    </form.Subscribe>
  );
}

function FormErrorBanner({ message }: { message: string | null }) {
  if (!message) return null;
  return <p className="mt-2 text-sm text-destructive">{message}</p>;
}

export const { useAppForm, withForm } = createFormHook({
  fieldContext,
  formContext,
  fieldComponents: {
    TextField,
    NumberField,
    SelectField,
    SelectFieldKV,
    ComboboxField,
    TextareaField,
    CheckboxField,
    DateField,
  },
  formComponents: {
    SubmitButton,
    CancelButton,
    FormActions,
    FormErrorBanner,
  },
});
