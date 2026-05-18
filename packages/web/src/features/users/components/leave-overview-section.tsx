import { useRouter } from '@tanstack/react-router';
import { LeaveAllowed, type UserLeave } from '#/api/user-leaves';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/hooks/use-form-server-errors';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { leavesFormSchema } from '#/features/users/schemas';
import { submitUpdateUserLeaves } from '#/features/users/server-fns';

export function LeaveOverviewSection({ userId, leaves }: { userId: string; leaves: UserLeave[] }) {
  const router = useRouter();
  const currentYear = new Date().getFullYear();

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
                <TableHead>Name</TableHead>
                <TableHead>Total</TableHead>
                <TableHead>Taken</TableHead>
                <TableHead>Balance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {leaves.map((l, i) => (
                <TableRow key={l.id}>
                  <TableCell>{l.leaveTypeName}</TableCell>
                  <TableCell>
                    {l.defaultAllowed === LeaveAllowed.Unlimited ? (
                      <span className="text-muted-foreground">Unlimited</span>
                    ) : (
                      <form.AppField name={`items[${i}].totalDays`}>
                        {(field) => <field.NumberField label="" min={0} />}
                      </form.AppField>
                    )}
                  </TableCell>
                  <TableCell>—</TableCell>
                  <TableCell>—</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <form.FormActions cancel />
        </form>
      </form.AppForm>
    </section>
  );
}
