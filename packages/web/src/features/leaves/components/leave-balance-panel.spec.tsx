// @vitest-environment jsdom
import { describe, it, expect } from 'vite-plus/test';
import { render } from '@testing-library/react';
import { LeaveBalancePanel } from './leave-balance-panel';
import type { LeaveSummaryResult } from '#/features/leaves/compute-leave-summary';

const LT_A = '00000000-0000-0000-0000-000000000001';
const LT_B = '00000000-0000-0000-0000-000000000002';

describe('LeaveBalancePanel', () => {
  it('renders a row per leave type plus Feestdagen', () => {
    const summary: LeaveSummaryResult = {
      rows: [
        { leaveTypeId: LT_A, name: 'Verlof', total: 20, taken: 5, balance: 15 },
        { leaveTypeId: LT_B, name: 'ADV', total: '—', taken: 2, balance: '—' },
        { leaveTypeId: '__feestdagen__', name: 'Feestdagen', total: '—', taken: 3, balance: '—' },
      ],
      totals: { total: 20, taken: 5, balance: 15 },
    };

    const { getByText } = render(<LeaveBalancePanel summary={summary} />);

    expect(getByText('Verlof')).toBeTruthy();
    expect(getByText('ADV')).toBeTruthy();
    expect(getByText('Feestdagen')).toBeTruthy();
  });

  it('renders — for unlimited total and balance', () => {
    const summary: LeaveSummaryResult = {
      rows: [
        { leaveTypeId: LT_B, name: 'ADV', total: '—', taken: 2, balance: '—' },
        { leaveTypeId: '__feestdagen__', name: 'Feestdagen', total: '—', taken: 3, balance: '—' },
      ],
      totals: { total: 0, taken: 0, balance: 0 },
    };

    const { getAllByText } = render(<LeaveBalancePanel summary={summary} />);

    // Multiple — expected: ADV total, ADV balance, Feestdagen total, Feestdagen balance
    const dashes = getAllByText('—');
    expect(dashes.length).toBeGreaterThanOrEqual(4);
  });

  it('renders numeric taken/balance for limited leave type', () => {
    const summary: LeaveSummaryResult = {
      rows: [
        { leaveTypeId: LT_A, name: 'Verlof', total: 20, taken: 3.5, balance: 16.5 },
        { leaveTypeId: '__feestdagen__', name: 'Feestdagen', total: '—', taken: 0, balance: '—' },
      ],
      totals: { total: 20, taken: 3.5, balance: 16.5 },
    };

    const { getAllByText } = render(<LeaveBalancePanel summary={summary} />);

    // 3.5 appears in both the row taken and totals footer taken
    const takenVals = getAllByText('3.5');
    expect(takenVals.length).toBeGreaterThanOrEqual(1);

    const balanceVals = getAllByText('16.5');
    expect(balanceVals.length).toBeGreaterThanOrEqual(1);
  });

  it('renders totals footer row with limited-only label', () => {
    const summary: LeaveSummaryResult = {
      rows: [
        { leaveTypeId: LT_A, name: 'Verlof', total: 20, taken: 5, balance: 15 },
        { leaveTypeId: '__feestdagen__', name: 'Feestdagen', total: '—', taken: 3, balance: '—' },
      ],
      totals: { total: 20, taken: 5, balance: 15 },
    };

    const { getAllByText } = render(<LeaveBalancePanel summary={summary} />);

    const totalLimitedLabels = getAllByText('Total (limited)');
    expect(totalLimitedLabels.length).toBeGreaterThanOrEqual(1);
  });

  it('renders 0 taken when no bookings exist', () => {
    const summary: LeaveSummaryResult = {
      rows: [
        { leaveTypeId: LT_A, name: 'Verlof', total: 20, taken: 0, balance: 20 },
        { leaveTypeId: '__feestdagen__', name: 'Feestdagen', total: '—', taken: 0, balance: '—' },
      ],
      totals: { total: 20, taken: 0, balance: 20 },
    };

    const { getAllByText } = render(<LeaveBalancePanel summary={summary} />);

    // At least one 0 in the table (taken cells)
    const zeros = getAllByText('0');
    expect(zeros.length).toBeGreaterThanOrEqual(1);
  });
});
