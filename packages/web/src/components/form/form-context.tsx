import { createFormHook, createFormHookContexts } from '@tanstack/react-form';
import type { ReactNode } from 'react';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { Textarea } from '#/components/ui/textarea';
import { Checkbox } from '#/components/ui/checkbox';
import { Button } from '#/components/ui/button';
import { FieldError } from '#/components/form/field-error';
import { hasClientSideError } from '#/lib/form-utils';

export const { fieldContext, formContext, useFieldContext, useFormContext } = createFormHookContexts();

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
        onChange={(e) => field.handleChange(e.target.value)}
      />
      <FieldError field={field} />
    </div>
  );
}

function NumberField({ label, suffix, min }: { label: ReactNode; suffix?: string; min?: number }) {
  const field = useFieldContext<number>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <div className="flex items-center gap-2">
        <Input
          id={field.name}
          name={field.name}
          type="number"
          min={min}
          value={field.state.value}
          aria-invalid={field.state.meta.isTouched && field.state.meta.errors.length > 0 ? true : undefined}
          onBlur={field.handleBlur}
          onChange={(e) => field.handleChange(Number(e.target.value))}
        />
        {suffix && <span className="text-sm text-muted-foreground">{suffix}</span>}
      </div>
      <FieldError field={field} />
    </div>
  );
}

function SelectField<T extends string>({ label, options }: { label: string; options: readonly T[] }) {
  const field = useFieldContext<T>();
  return (
    <div className="grid gap-2">
      <Label htmlFor={field.name}>{label}</Label>
      <select
        id={field.name}
        name={field.name}
        value={field.state.value}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value as T)}
        className="border-input file:text-foreground placeholder:text-muted-foreground selection:bg-primary selection:text-primary-foreground dark:bg-input/30 flex h-9 w-full min-w-0 rounded-md border bg-transparent px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] md:text-sm"
      >
        {options.map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>
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
        onChange={(e) => field.handleChange(e.target.value)}
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
        onCheckedChange={(checked) => field.handleChange(checked === true ? true : false)}
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
        onChange={(e) => field.handleChange(e.target.value)}
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
        hasClientError: hasClientSideError(state.fieldMeta),
        isSubmitting: state.isSubmitting,
        isChanged: !state.isDefaultValue,
      })}
    >
      {({ hasClientError, isSubmitting, isChanged }) => (
        <Button type="submit" disabled={hasClientError || isSubmitting || !isChanged}>
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
        hasClientError: hasClientSideError(state.fieldMeta),
        isSubmitting: state.isSubmitting,
        isChanged: !state.isDefaultValue,
      })}
    >
      {({ hasClientError, isSubmitting, isChanged }) => (
        <div className={className}>
          <Button type="submit" disabled={hasClientError || isSubmitting || !isChanged}>
            {isSubmitting ? savePendingLabel : saveLabel}
          </Button>
          {showCancel && (customCancel || isChanged) && (
            <Button
              type="button"
              variant="outline"
              onClick={() => (customCancel ? customCancel() : form.reset())}
            >
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
