type FieldLike = {
  state: {
    meta: {
      isTouched: boolean;
      errors: Array<unknown>;
    };
  };
};

export function FieldError({ field }: { field: FieldLike }) {
  if (!field.state.meta.isTouched || field.state.meta.errors.length === 0) return null;
  const message = field.state.meta.errors
    .map((err) => (typeof err === 'string' ? err : (err as { message?: string })?.message))
    .filter(Boolean)
    .join(', ');
  if (!message) return null;
  return <p className="text-sm text-destructive">{message}</p>;
}
