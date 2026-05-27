export function isoWeekToMonday(year: number, week: number): Date {
  const jan4 = new Date(year, 0, 4);
  const dayOfWeek = jan4.getDay() || 7;
  const monday = new Date(jan4);
  monday.setDate(jan4.getDate() - (dayOfWeek - 1) + (week - 1) * 7);
  return monday;
}

export function dateToIsoWeek(date: Date): { year: number; week: number } {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  const week = Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
  return { year: d.getUTCFullYear(), week };
}

export function formatIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function prevWeek(year: number, week: number): { year: number; week: number } {
  const monday = isoWeekToMonday(year, week);
  monday.setDate(monday.getDate() - 7);
  return dateToIsoWeek(monday);
}

export function nextWeek(year: number, week: number): { year: number; week: number } {
  const monday = isoWeekToMonday(year, week);
  monday.setDate(monday.getDate() + 7);
  return dateToIsoWeek(monday);
}

export function todayWeek(): { year: number; week: number } {
  return dateToIsoWeek(new Date());
}

const DAY_NAMES = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export function formatDayHeader(isoDate: string): { day: string; label: string } {
  const d = new Date(isoDate + 'T00:00:00');
  return {
    day: DAY_NAMES[d.getDay() === 0 ? 6 : d.getDay() - 1],
    label: `${d.getDate()} ${MONTH_NAMES[d.getMonth()]}`,
  };
}

export type DurationValidation = 'valid' | 'exceeds' | 'invalid';

export function validateDurationInput(raw: string): DurationValidation {
  const n = parseFloat(raw.replace(',', '.'));
  if (isNaN(n)) return 'invalid';
  if (n < 0.25) return 'invalid';
  if (Math.round(n * 4) !== n * 4) return 'invalid';
  if (n > 8) return 'exceeds';
  return 'valid';
}

export function parseDurationInput(raw: string): number | null {
  if (validateDurationInput(raw) !== 'valid') return null;
  return parseFloat(raw.replace(',', '.'));
}

/**
 * Display value used for totals (day / row / week). Prefers the user's raw
 * input — even when it doesn't satisfy {@link validateDurationInput} — so
 * totals reflect what the viewer sees in the cells. Falls back to the
 * committed stored value when no raw input exists or it can't be parsed.
 */
export function displayDuration(raw: string | undefined, stored: number | undefined): number {
  if (raw !== undefined && raw !== '') {
    const parsed = parseFloat(raw.replace(',', '.'));
    if (!isNaN(parsed)) return parsed;
  }
  return stored ?? 0;
}

export function isValidDuration(val: number): boolean {
  return val >= 0.25 && val <= 8 && Math.round(val * 4) === val * 4;
}

export function prevMonth(year: number, month: number): { year: number; month: number } {
  if (month === 1) return { year: year - 1, month: 12 };
  return { year, month: month - 1 };
}

export function nextMonth(year: number, month: number): { year: number; month: number } {
  if (month === 12) return { year: year + 1, month: 1 };
  return { year, month: month + 1 };
}

export function todayMonth(): { year: number; month: number } {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

export function formatMonthLabel(year: number, month: number): string {
  return new Date(year, month - 1, 1).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
}
