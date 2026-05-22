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
  state: {
    value: ContractTaskFormValue[];
    meta: { errors: Array<unknown> };
  };
  pushValue: (value: ContractTaskFormValue) => void;
  removeValue: (index: number) => void;
};

type AnyFieldComponents = {
  TextField: (props: { label: string; hideLabel?: boolean; disabled?: boolean }) => React.ReactElement;
  NumberField: (props: { label: string; step?: number; min?: number; suffix?: string }) => React.ReactElement;
  DecimalField: (props: {
    label: string;
    min?: number;
    max?: number;
    decimals?: number;
    suffix?: string;
    hideLabel?: boolean;
    disabled?: boolean;
  }) => React.ReactElement;
};

export function ContractTaskSubform({
  form,
  archived,
  disabled,
}: {
  form: AppForm;
  archived: ContractTask[];
  disabled?: boolean;
}) {
  return (
    <section className="grid gap-4">
      <h2 className="text-lg font-semibold">Tasks</h2>

      <form.Field name="tasks">
        {(field) => {
          const broughtBackIds = new Set(field.state.value.map((t) => t.originalArchivedId).filter(Boolean));
          const visibleArchived = archived.filter((t) => !broughtBackIds.has(t.id));
          const arrayErrors = field.state.meta.errors
            .map((err) => (typeof err === 'string' ? err : (err as { message?: string })?.message))
            .filter((m): m is string => Boolean(m));

          return (
            <div className="grid gap-3">
              {arrayErrors.length > 0 && (
                <p className="text-sm text-destructive" aria-live="polite">
                  {arrayErrors.join(', ')}
                </p>
              )}
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
                        {(f) => <f.TextField label="Name" hideLabel disabled={disabled} />}
                      </form.AppField>
                      <form.AppField name={`tasks[${i}].rate`}>
                        {(f) => <f.DecimalField label="Rate €" min={0} decimals={2} hideLabel disabled={disabled} />}
                      </form.AppField>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label="Remove task"
                        disabled={disabled}
                        onClick={() => field.removeValue(i)}
                        className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                      >
                        <XIcon className="size-5" />
                      </Button>
                    </div>
                  ))}
                </div>
              )}
              {!disabled && (
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
              )}

              {visibleArchived.length > 0 && (
                <div className="grid gap-2 border-t pt-4">
                  <h3 className="text-sm font-medium text-muted-foreground">Archived</h3>
                  <ul className="grid gap-1 text-sm">
                    {visibleArchived.map((t) => (
                      <li key={t.id} className="grid grid-cols-[1fr_120px_140px_auto] items-center gap-3">
                        <span>{t.name}</span>
                        <span className="text-muted-foreground">{rateFmt.format(t.rate)} €</span>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={disabled || broughtBackIds.has(t.id)}
                          onClick={() =>
                            field.pushValue({ id: null, name: t.name, rate: t.rate, originalArchivedId: t.id })
                          }
                        >
                          Bring back
                        </Button>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          );
        }}
      </form.Field>
    </section>
  );
}
