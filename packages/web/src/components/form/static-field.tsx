import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';

// Read-only display field for values that aren't form-bound (e.g. a disabled
// email/customer-number). Mirrors TextField's layout — including the reserved
// error-line spacer — so it lines up with sibling fields in a `gap`-spaced form.
export function StaticField({ id, label, value }: { id: string; label: string; value: string | number }) {
  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} disabled />
      <p className="min-h-[1.25rem]" aria-hidden="true" />
    </div>
  );
}
