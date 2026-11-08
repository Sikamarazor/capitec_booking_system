import {
  addDays,
  compareDates,
  dayParts,
  formatLongDate,
  formatSlotRange,
  formatTime,
  formatWhen,
  sameInstant,
  todayIn,
} from './branch-time';

describe('branch-time', () => {
  const sixAmUtc = '2026-11-03T06:00:00+00:00';

  it('todayIn gives the calendar date at the place, not in UTC', () => {
    const lateEveningUtc = new Date('2026-11-02T22:30:00Z');

    expect(todayIn('Africa/Johannesburg', lateEveningUtc)).toBe('2026-11-03');
    expect(todayIn('Europe/London', lateEveningUtc)).toBe('2026-11-02');
  });

  it('addDays crosses month and year ends', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-11-01', -1)).toBe('2026-10-31');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-11-03', 7)).toBe('2026-11-10');
  });

  it('compareDates orders calendar dates', () => {
    expect(compareDates('2026-11-03', '2026-11-04')).toBe(-1);
    expect(compareDates('2026-11-04', '2026-11-03')).toBe(1);
    expect(compareDates('2026-11-03', '2026-11-03')).toBe(0);
  });

  it('formatTime shows the wall clock of the given zone', () => {
    expect(formatTime(sixAmUtc, 'Africa/Johannesburg')).toBe('08:00');
    expect(formatTime(sixAmUtc, 'America/New_York')).toBe('01:00');
  });

  it('formatLongDate names the weekday and the full date', () => {
    const text = formatLongDate(sixAmUtc, 'Africa/Johannesburg');

    expect(text).toContain('Tuesday');
    expect(text).toContain('3 November 2026');
  });

  it('formatLongDate follows the zone across midnight', () => {
    // 23:00 UTC on the 3rd is already the 4th in Johannesburg.
    expect(formatLongDate('2026-11-03T23:00:00+00:00', 'Africa/Johannesburg')).toContain('4 November 2026');
  });

  it('formatSlotRange and formatWhen combine the pieces', () => {
    expect(formatSlotRange(sixAmUtc, '2026-11-03T06:30:00+00:00', 'Africa/Johannesburg')).toBe('08:00 to 08:30');
    expect(formatWhen(sixAmUtc, 'Africa/Johannesburg')).toContain('at 08:00');
  });

  it('dayParts labels a calendar date for the date strip', () => {
    expect(dayParts('2026-11-03')).toEqual({ weekday: 'Tue', day: '3', month: 'Nov' });
  });

  it('sameInstant compares moments, not text', () => {
    expect(sameInstant('2026-11-03T06:00:00+00:00', '2026-11-03T08:00:00+02:00')).toBe(true);
    expect(sameInstant('2026-11-03T06:00:00+00:00', '2026-11-03T06:30:00+00:00')).toBe(false);
    expect(sameInstant(null, '2026-11-03T06:00:00+00:00')).toBe(false);
  });
});
