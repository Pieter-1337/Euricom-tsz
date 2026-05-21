import { createFormHook, createFormHookContexts } from '@tanstack/react-form';
import { type ReactNode } from 'react';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { Textarea } from '#/components/ui/textarea';
import { Checkbox } from '#/components/ui/checkbox';
import { Button } from '#/components/ui/button';
import { SelectInput, type SelectOption } from '#/components/ui/select-input';
import { Combobox, type ComboboxOption } from '#/components/ui/combobox';
import { MultiCombobox, type MultiComboboxOption } from '#/components/ui/multi-combobox';
import { NumberInput } from '#/components/ui/number-input';
import { DecimalInput } from '#/components/ui/decimal-input';
import { DatePicker } from '#/components/ui/date-picker';
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

function TextField({ label, type, hideLabel }: { label: string; type?: string; hideLabel?: boolean }) {
  const field = useFieldContext<string>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name} className={hideLabel ? 'sr-only' : undefined}>
        {label}
      </Label>
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
  step,
}: {
  label: ReactNode;
  suffix?: string;
  min?: number;
  max?: number;
  step?: number;
}) {
  const field = useFieldContext<number>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <div className="flex items-center gap-2">
        <NumberInput
          id={field.name}
          name={field.name}
          value={field.state.value}
          min={min}
          max={max}
          step={step}
          aria-invalid={hasError ? true : undefined}
          className="flex-1"
          onBlur={field.handleBlur}
          onChange={(n) => {
            clearServerErrorFor(field);
            field.handleChange(n);
          }}
        />
        {suffix && <span className="text-sm text-muted-foreground">{suffix}</span>}
      </div>
      <FieldError field={field} />
    </div>
  );
}

function DecimalField({
  label,
  suffix,
  min,
  max,
  decimals,
  hideLabel,
}: {
  label: ReactNode;
  suffix?: string;
  min?: number;
  max?: number;
  decimals?: number;
  hideLabel?: boolean;
}) {
  const field = useFieldContext<number>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name} className={hideLabel ? 'sr-only' : undefined}>
        {label}
      </Label>
      <div className="flex items-center gap-2">
        <DecimalInput
          id={field.name}
          name={field.name}
          value={field.state.value}
          min={min}
          max={max}
          decimals={decimals}
          aria-invalid={hasError ? true : undefined}
          className="flex-1"
          onBlur={field.handleBlur}
          onChange={(n) => {
            clearServerErrorFor(field);
            field.handleChange(n);
          }}
        />
        {suffix && <span className="text-sm text-muted-foreground">{suffix}</span>}
      </div>
      <FieldError field={field} />
    </div>
  );
}

function SelectField<T extends string>({
  label,
  options,
  placeholder,
}: {
  label: string;
  options: readonly SelectOption<T>[];
  placeholder?: string;
}) {
  const field = useFieldContext<T>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <SelectInput
        id={field.name}
        name={field.name}
        value={field.state.value}
        options={options}
        placeholder={placeholder ?? `Select ${label.toLowerCase()}`}
        aria-invalid={hasError ? true : undefined}
        onBlur={field.handleBlur}
        onValueChange={(v) => {
          clearServerErrorFor(field);
          field.handleChange(v);
        }}
      />
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

function MultiSelectField<T extends string>({
  label,
  options,
  placeholder,
  searchPlaceholder,
  emptyMessage,
}: {
  label: string;
  options: readonly MultiComboboxOption[] | readonly T[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyMessage?: string;
}) {
  const field = useFieldContext<T[]>();
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  const normalizedOptions: readonly MultiComboboxOption[] = options.map((o) =>
    typeof o === 'string' ? { value: o, label: o } : o,
  );
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <MultiCombobox
        id={field.name}
        name={field.name}
        value={field.state.value ?? []}
        onValueChange={(next) => {
          clearServerErrorFor(field);
          field.handleChange(next as T[]);
        }}
        onBlur={field.handleBlur}
        aria-invalid={hasError ? true : undefined}
        options={normalizedOptions}
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
    <div className="grid gap-2 self-start">
      <Label htmlFor={field.name}>{label}</Label>
      <DatePicker
        id={field.name}
        name={field.name}
        value={field.state.value}
        aria-invalid={field.state.meta.isTouched && field.state.meta.errors.length > 0 ? true : undefined}
        onBlur={field.handleBlur}
        onChange={(value) => {
          clearServerErrorFor(field);
          field.handleChange(value);
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
    DecimalField,
    SelectField,
    ComboboxField,
    MultiSelectField,
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
