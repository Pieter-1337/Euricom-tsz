type FieldLike = {
  state: {
    meta: {
      isTouched: boolean;
      errors: Array<unknown>;
    };
  };
};

export function FieldError({ field }: { field: FieldLike }) {
  const hasError = field.state.meta.isTouched && field.state.meta.errors.length > 0;
  const message = hasError
    ? field.state.meta.errors
        .map((err) => (typeof err === 'string' ? err : (err as { message?: string })?.message))
        .filter(Boolean)
        .join(', ')
    : '';
  return (
    <p className="min-h-[1.25rem] text-sm text-destructive" aria-live="polite">
      {message}
    </p>
  );
}
