const LOCALE = 'en-GB';

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** Today's calendar date at a place, as `YYYY-MM-DD`. */
export function todayIn(timeZone: string, now: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat(LOCALE, { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(now);
  const get = (type: string): string => parts.find((p) => p.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}`;
}

/** Calendar arithmetic on `YYYY-MM-DD` strings; no time zones involved. */
export function addDays(date: string, days: number): string {
  const [year, month, day] = date.split('-').map(Number);
  const moved = new Date(Date.UTC(year, month - 1, day + days));
  return `${moved.getUTCFullYear()}-${pad(moved.getUTCMonth() + 1)}-${pad(moved.getUTCDate())}`;
}

export function compareDates(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

export interface DayParts {
  weekday: string;
  day: string;
  month: string;
}

/** Labels for a calendar date, for the date strip. */
export function dayParts(date: string): DayParts {
  const noon = new Date(`${date}T12:00:00Z`);
  const format = (options: Intl.DateTimeFormatOptions): string =>
    new Intl.DateTimeFormat(LOCALE, { timeZone: 'UTC', ...options }).format(noon);
  return { weekday: format({ weekday: 'short' }), day: format({ day: 'numeric' }), month: format({ month: 'short' }) };
}

/** `Tuesday 3 November 2026` (the comma after the weekday depends on the runtime's ICU version). */
export function formatLongDate(isoInstant: string, timeZone: string): string {
  return new Intl.DateTimeFormat(LOCALE, {
    timeZone,
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(new Date(isoInstant));
}

/** `08:00` on the wall clock of `timeZone`. */
export function formatTime(isoInstant: string, timeZone: string): string {
  return new Intl.DateTimeFormat(LOCALE, { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(
    new Date(isoInstant),
  );
}

/** `08:00 to 08:30`. */
export function formatSlotRange(startIso: string, endIso: string, timeZone: string): string {
  return `${formatTime(startIso, timeZone)} to ${formatTime(endIso, timeZone)}`;
}

/** `Tuesday 3 November 2026 at 08:00`. */
export function formatWhen(isoInstant: string, timeZone: string): string {
  return `${formatLongDate(isoInstant, timeZone)} at ${formatTime(isoInstant, timeZone)}`;
}

/** True when two ISO instants are the same moment, whatever offset notation each uses. */
export function sameInstant(a: string | null | undefined, b: string | null | undefined): boolean {
  return !!a && !!b && Date.parse(a) === Date.parse(b);
}
