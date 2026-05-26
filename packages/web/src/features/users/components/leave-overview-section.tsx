import { useRouter } from '@tanstack/react-router';
import { LeaveAllowed, type UserLeave } from '#/api/user-leaves';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { leavesFormSchema } from '#/features/users/schemas';
import { submitUpdateUserLeaves } from '#/features/users/server-fns';
import { useLeaveSummary } from '#/features/leaves/use-leave-summary';
import type { DisplayValue } from '#/features/leaves/compute-leave-summary';

function DisplayCell({ value }: { value: DisplayValue }) {
  if (value === '—') return <span className="text-muted-foreground">—</span>;
  return <span>{Math.round(value * 100) / 100}</span>;
}

export function LeaveOverviewSection({ userId, leaves }: { userId: string; leaves: UserLeave[] }) {
  const router = useRouter();
  const currentYear = new Date().getFullYear();
  const { summary } = useLeaveSummary(userId, currentYear);

  const summaryByLeaveTypeId = new Map(summary.rows.map((r) => [r.leaveTypeId, r]));

  const toFormItems = (rows: UserLeave[]) =>
    rows.map((l) => ({ id: l.id, leaveTypeId: l.leaveTypeId, totalDays: l.totalDays }));

  const form = useAppForm({
    defaultValues: { items: toFormItems(leaves) },
    validators: { onChange: leavesFormSchema },
    onSubmit: async ({ value }) => {
      clearServerErrors();
      try {
        const updated = await submitUpdateUserLeaves({
          data: { userId, body: { year: currentYear, items: value.items } },
        });
        form.reset({ items: toFormItems(updated) });
        await router.invalidate();
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, ['items']);

  const feestdagenRow = summary.rows.find((r) => r.leaveTypeId === '__feestdagen__');

  return (
    <section className="mt-8">
      <h2 className="mb-4 text-lg font-semibold">Leave overview — {currentYear}</h2>
      <form.AppForm>
        <form.FormErrorBanner message={serverError} />
        <form
          onSubmit={(e) => {
            e.preventDefault();
            e.stopPropagation();
            form.handleSubmit();
          }}
        >
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-1/4">Name</TableHead>
                <TableHead className="w-1/4">Total</TableHead>
                <TableHead className="w-1/4">Taken</TableHead>
                <TableHead className="w-1/4">Balance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {leaves.map((l, i) => {
                const row = summaryByLeaveTypeId.get(l.leaveTypeId);
                return (
                  <TableRow key={l.id} className="h-14">
                    <TableCell>{l.leaveTypeName}</TableCell>
                    <TableCell>
                      {l.defaultAllowed === LeaveAllowed.Unlimited ? (
                        <span className="text-muted-foreground">Unlimited</span>
                      ) : (
                        <div className="w-28 [&_label]:sr-only [&_p]:hidden">
                          <form.AppField name={`items[${i}].totalDays`}>
                            {(field) => <field.NumberField label="" min={0} />}
                          </form.AppField>
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      <DisplayCell value={row?.taken ?? '—'} />
                    </TableCell>
                    <TableCell>
                      <DisplayCell value={row?.balance ?? '—'} />
                    </TableCell>
                  </TableRow>
                );
              })}
              {feestdagenRow && (
                <TableRow className="h-14">
                  <TableCell>{feestdagenRow.name}</TableCell>
                  <TableCell>
                    <DisplayCell value={feestdagenRow.total} />
                  </TableCell>
                  <TableCell>
                    <DisplayCell value={feestdagenRow.taken} />
                  </TableCell>
                  <TableCell>
                    <DisplayCell value={feestdagenRow.balance} />
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell className="font-semibold">Totals (limited)</TableCell>
                <TableCell className="font-semibold">{Math.round(summary.totals.total * 100) / 100}</TableCell>
                <TableCell className="font-semibold">{Math.round(summary.totals.taken * 100) / 100}</TableCell>
                <TableCell className="font-semibold">{Math.round(summary.totals.balance * 100) / 100}</TableCell>
              </TableRow>
            </TableFooter>
          </Table>
          <form.FormActions cancel />
        </form>
      </form.AppForm>
    </section>
  );
}
