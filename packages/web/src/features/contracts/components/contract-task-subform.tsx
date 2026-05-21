import { XIcon } from 'lucide-react';
import { Button } from '#/components/ui/button';
import type { ContractTask } from '#/api/contracts';
import type { ContractTaskFormValue } from '#/features/contracts/schemas';

const rateFmt = new Intl.NumberFormat('nl-BE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

type AppForm = {
  Field: (props: { name: 'tasks'; children: (field: ArrayFieldApi) => React.ReactNode }) => React.ReactNode;
  AppField: (props: { name: string; children: (field: AnyFieldComponents) => React.ReactNode }) => React.ReactNode;
  pushFieldValue: (name: 'tasks', value: ContractTaskFormValue) => void;
};

type ArrayFieldApi = {
  state: { value: ContractTaskFormValue[] };
  pushValue: (value: ContractTaskFormValue) => void;
  removeValue: (index: number) => void;
};

type AnyFieldComponents = {
  TextField: (props: { label: string; hideLabel?: boolean }) => React.ReactElement;
  NumberField: (props: { label: string; step?: number; min?: number; suffix?: string }) => React.ReactElement;
  DecimalField: (props: {
    label: string;
    min?: number;
    max?: number;
    decimals?: number;
    suffix?: string;
    hideLabel?: boolean;
  }) => React.ReactElement;
};

export function ContractTaskSubform({ form, archived }: { form: AppForm; archived: ContractTask[] }) {
  return (
    <section className="grid gap-4">
      <h2 className="text-lg font-semibold">Tasks</h2>

      <form.Field name="tasks">
        {(field) => (
          <div className="grid gap-3">
            {field.state.value.length === 0 ? (
              <p className="text-sm text-muted-foreground">No tasks yet.</p>
            ) : (
              <div className="grid gap-2">
                <div className="grid grid-cols-[1fr_140px_36px] items-center gap-3 text-sm font-medium">
                  <span>Name</span>
                  <span>Rate €</span>
                  <span />
                </div>
                {field.state.value.map((_task, i) => (
                  <div key={i} className="grid grid-cols-[1fr_140px_36px] items-start gap-3">
                    <form.AppField name={`tasks[${i}].name`}>
                      {(f) => <f.TextField label="Name" hideLabel />}
                    </form.AppField>
                    <form.AppField name={`tasks[${i}].rate`}>
                      {(f) => <f.DecimalField label="Rate €" min={0} decimals={2} hideLabel />}
                    </form.AppField>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label="Remove task"
                      onClick={() => field.removeValue(i)}
                      className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                    >
                      <XIcon className="size-5" />
                    </Button>
                  </div>
                ))}
              </div>
            )}
            <div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => field.pushValue({ id: null, name: '', rate: 0 })}
              >
                Add task
              </Button>
            </div>
          </div>
        )}
      </form.Field>

      {archived.length > 0 && (
        <div className="grid gap-2 border-t pt-4">
          <h3 className="text-sm font-medium text-muted-foreground">Archived</h3>
          <ul className="grid gap-1 text-sm">
            {archived.map((t) => (
              <li key={t.id} className="grid grid-cols-[1fr_120px_140px_auto] items-center gap-3">
                <span>{t.name}</span>
                <span className="text-muted-foreground">{rateFmt.format(t.rate)} €</span>
                <span className="text-muted-foreground">{formatDate(t.deletedAt)}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => form.pushFieldValue('tasks', { id: null, name: t.name, rate: t.rate })}
                >
                  Bring back
                </Button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function formatDate(iso: string | null): string {
  if (!iso) return '';
  return iso.slice(0, 10);
}
