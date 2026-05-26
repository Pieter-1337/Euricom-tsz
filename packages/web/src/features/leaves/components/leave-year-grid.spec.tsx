// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from 'vite-plus/test';
import { render, cleanup } from '@testing-library/react';

// Mock the router Link as a plain anchor whose href is built from `to` + `params`.
// This mirrors the repo's component-test convention (see timesheet-week-grid.spec)
// and keeps the click-target URL assertion meaningful without standing up a full
// router (which renders nothing synchronously and doesn't know the app's routes).
vi.mock('@tanstack/react-router', () => ({
  Link: ({
    to,
    params,
    children,
    ...rest
  }: {
    to: string;
    params?: Record<string, string>;
    children: React.ReactNode;
  } & Record<string, unknown>) => {
    let href = to;
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        href = href.replace(`$${key}`, value);
      }
    }
    return (
      <a href={href} {...rest}>
        {children}
      </a>
    );
  },
}));

import { LeaveYearGrid } from './leave-year-grid';
import type { LeaveBookingForYear, HolidayDto } from '#/api/leaves';

const withRouter = (ui: React.ReactElement) => render(ui);

const YEAR = 2026;

const LT_A = '00000000-0000-0000-0000-000000000001';
const LT_B = '00000000-0000-0000-0000-000000000002';

const makeBooking = (
  date: string,
  leaveTypeId: string,
  leaveTypeName: string,
  durationHours = 8,
): LeaveBookingForYear => ({ date, leaveTypeId, leaveTypeName, durationHours });

const makeHoliday = (date: string, name = 'Holiday'): HolidayDto => ({
  date,
  name,
  type: 'Public',
});

describe('LeaveYearGrid', () => {
  afterEach(() => cleanup());

  it('renders 12 month sections', () => {
    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={[]} holidays={[]} />);

    const sections = container.querySelectorAll('section');
    expect(sections.length).toBe(12);
  });

  it('renders day-of-week header labels for each month', () => {
    const { getAllByText } = withRouter(<LeaveYearGrid year={YEAR} bookings={[]} holidays={[]} />);

    // 12 months × 7 day labels → 12 'Mo' labels
    const moLabels = getAllByText('Mo');
    expect(moLabels.length).toBe(12);
  });

  it('adds ring-2 ring-inset to today cell', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-15T12:00:00Z'));

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={[]} holidays={[]} />);

    const ringEl = container.querySelector('.ring-2');
    expect(ringEl).not.toBeNull();

    vi.useRealTimers();
  });

  it('renders booking colour for a single-type booking day', () => {
    const bookings = [makeBooking('2026-03-10', LT_A, 'Verlof', 8)];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={bookings} holidays={[]} />);

    // At least one coloured div from the palette
    const coloured = container.querySelector(
      '[class*="bg-amber-"], [class*="bg-blue-"], [class*="bg-violet-"], [class*="bg-rose-"], [class*="bg-cyan-"], [class*="bg-orange-"], [class*="bg-indigo-"], [class*="bg-teal-"]',
    );
    expect(coloured).not.toBeNull();
  });

  it('renders top/bottom split for 2-type day', () => {
    const bookings = [
      makeBooking('2026-04-20', LT_A, 'Verlof', 8),
      makeBooking('2026-04-20', LT_B, 'ADV', 8),
    ];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={bookings} holidays={[]} />);

    // The split day cell has 2 half-height divs
    const halfDivs = container.querySelectorAll('.h-1\\/2');
    expect(halfDivs.length).toBeGreaterThanOrEqual(2);
  });

  it('renders hatched pattern for 3+ type day', () => {
    const LT_C = '00000000-0000-0000-0000-000000000003';
    const bookings = [
      makeBooking('2026-05-05', LT_A, 'Verlof', 8),
      makeBooking('2026-05-05', LT_B, 'ADV', 8),
      makeBooking('2026-05-05', LT_C, 'Sick', 8),
    ];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={bookings} holidays={[]} />);

    // Hatched cell uses inline backgroundImage with repeating-linear-gradient
    const hatchedEl = container.querySelector('[style*="repeating-linear-gradient"]');
    expect(hatchedEl).not.toBeNull();
  });

  it('renders half-day visual modifier for bookings < 8 hours', () => {
    const bookings = [makeBooking('2026-07-06', LT_A, 'Verlof', 4)];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={bookings} holidays={[]} />);

    // Half-day rendering adds bg-white/30 overlay div
    const halfOverlay = container.querySelector('.bg-white\\/30');
    expect(halfOverlay).not.toBeNull();
  });

  it('click target link navigates to correct ISO week URL for 2026-01-05 (W02)', () => {
    // 2026-01-05 is Monday of ISO week 2 of 2026
    const bookings = [makeBooking('2026-01-05', LT_A, 'Verlof', 8)];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={bookings} holidays={[]} />);

    // Day cells are links — find one pointing to week 2
    const links = container.querySelectorAll('a[href]');
    const weekLinks = Array.from(links).filter((el) => {
      const href = el.getAttribute('href') ?? '';
      return href.includes('/time-entry/week/2026/2');
    });
    expect(weekLinks.length).toBeGreaterThan(0);
  });

  it('holiday cells have a title attribute with the holiday name', () => {
    const holidays = [makeHoliday('2026-01-01', 'Nieuwjaar')];

    const { container } = withRouter(<LeaveYearGrid year={YEAR} bookings={[]} holidays={holidays} />);

    const holidayLink = container.querySelector('[title="Nieuwjaar"]');
    expect(holidayLink).not.toBeNull();
  });
});
