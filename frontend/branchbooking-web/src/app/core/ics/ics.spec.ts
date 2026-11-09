import { aBooking } from '../../testing/api-fixtures';
import { buildIcs, escapeIcsText, foldLine, toIcsUtc } from './ics';

const unfold = (text: string): string => text.replace(/\r\n /g, '');
const octets = (text: string): number => new TextEncoder().encode(text).length;

describe('ics', () => {
  const options = { uid: 'test-uid@branchbooking.invalid', now: new Date('2026-11-02T07:30:00Z') };

  it('builds a calendar with CRLF line endings and the required structure', () => {
    const text = buildIcs(aBooking(), options);

    expect(text.startsWith('BEGIN:VCALENDAR\r\nVERSION:2.0\r\n')).toBe(true);
    expect(text.endsWith('END:VEVENT\r\nEND:VCALENDAR\r\n')).toBe(true);
    expect(text.replace(/\r\n/g, '')).not.toMatch(/[\r\n]/);
    expect(text).toContain('UID:test-uid@branchbooking.invalid');
    expect(text).toContain('DTSTAMP:20261102T073000Z');
  });

  it('writes the start and end as UTC instants, whatever offset the API used', () => {
    const text = buildIcs(aBooking({ startsAtUtc: '2026-11-03T08:00:00+02:00', endsAtUtc: '2026-11-03T08:30:00+02:00' }), options);

    expect(text).toContain('DTSTART:20261103T060000Z');
    expect(text).toContain('DTEND:20261103T063000Z');
    expect(toIcsUtc('2026-11-03T06:00:00.000+00:00')).toBe('20261103T060000Z');
  });

  it('puts the branch wall-clock time, what to bring and the reference in the description', () => {
    const text = unfold(buildIcs(aBooking(), options));

    expect(text).toContain('LOCATION:Test Branch\\, 1 Test Street\\, Johannesburg\\, Gauteng');
    expect(text).toContain('at 08:00.');
    expect(text).toContain('What to bring: Bring your ID document.');
    expect(text).toContain('Booking reference: BK-7H2QX-M9RTD');
  });

  it('escapes backslashes, semicolons, commas and new lines in text', () => {
    expect(escapeIcsText('a;b,c\\d\ne')).toBe('a\\;b\\,c\\\\d\\ne');

    const text = unfold(
      buildIcs(aBooking({ appointmentType: { ...aBooking().appointmentType, whatToBring: 'ID; proof, of address' } }), options),
    );
    expect(text).toContain('ID\\; proof\\, of address');
  });

  it('folds long lines at 75 octets and unfolding gives the original back', () => {
    const long = 'X'.repeat(200);
    const folded = foldLine(long);

    for (const physical of folded.split('\r\n')) {
      expect(octets(physical) > 75).toBe(false);
    }
    expect(unfold(folded)).toBe(long);
  });

  it('never splits a multi-byte character when folding', () => {
    const long = 'é'.repeat(100) + '🙂'.repeat(20);
    const folded = foldLine(long);

    for (const physical of folded.split('\r\n')) {
      expect(octets(physical) > 75).toBe(false);
    }
    expect(unfold(folded)).toBe(long);
  });

  it('does not put the reference in the event id', () => {
    const text = buildIcs(aBooking());

    expect(text).toMatch(/UID:[^\r\n]+@branchbooking\.invalid/);
    expect(text.split('\r\n').find((line) => line.startsWith('UID:'))).not.toContain('BK-7H2QX-M9RTD');
  });
});
