import { Booking } from '../api/api.models';
import { formatWhen } from '../time/branch-time';

export const ICS_FILE_NAME = 'appointment.ics';
export const ICS_MIME_TYPE = 'text/calendar;charset=utf-8';

const CRLF = '\r\n';
const MAX_LINE_OCTETS = 75;
const encoder = new TextEncoder();

/** RFC 5545 section 3.3.11: escape backslash, semicolon, comma and line breaks in text values. */
export function escapeIcsText(value: string): string {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,')
    .replace(/\r\n|\r|\n/g, '\\n');
}

/** `2026-11-03T08:00:00+02:00` becomes `20261103T060000Z`. */
export function toIcsUtc(isoInstant: string): string {
  return new Date(isoInstant).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
}

/**
 * RFC 5545 section 3.1: no physical line is longer than 75 octets. A longer line is continued on the next line,
 * which starts with one space. Octets are UTF-8 bytes, and a character is never split.
 */
export function foldLine(line: string): string {
  const parts: string[] = [];
  let current = '';
  let size = 0;
  let limit = MAX_LINE_OCTETS;

  for (const character of line) {
    const bytes = encoder.encode(character).length;
    if (size + bytes > limit) {
      parts.push(current);
      current = '';
      size = 0;
      limit = MAX_LINE_OCTETS - 1; // a continuation line spends one octet on its leading space
    }
    current += character;
    size += bytes;
  }

  parts.push(current);
  return parts.join(`${CRLF} `);
}

export interface IcsOptions {
  /** Overrides the random event id (for tests). */
  uid?: string;
  /** Overrides the time stamp (for tests). */
  now?: Date;
}

function newUid(): string {
  const random = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  return `${random}@branchbooking.invalid`;
}

/** One-event calendar file for a booking. Contains the reference in the description (Epic 05, open item 14). */
export function buildIcs(booking: Booking, options: IcsOptions = {}): string {
  const zone = booking.branch.timeZoneId;
  const location = [booking.branch.name, booking.branch.addressLine, booking.branch.city, booking.branch.province]
    .filter((part) => part.length > 0)
    .join(', ');
  const description = [
    `${booking.appointmentType.name} at ${booking.branch.name}.`,
    `Time at the branch: ${formatWhen(booking.startsAtUtc, zone)}.`,
    booking.appointmentType.whatToBring ? `What to bring: ${booking.appointmentType.whatToBring}` : null,
    `Booking reference: ${booking.reference}`,
  ]
    .filter((line): line is string => line !== null)
    .join('\n');

  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Branch Booking//Appointment//EN',
    'CALSCALE:GREGORIAN',
    'METHOD:PUBLISH',
    'BEGIN:VEVENT',
    `UID:${options.uid ?? newUid()}`,
    `DTSTAMP:${toIcsUtc((options.now ?? new Date()).toISOString())}`,
    `DTSTART:${toIcsUtc(booking.startsAtUtc)}`,
    `DTEND:${toIcsUtc(booking.endsAtUtc)}`,
    `SUMMARY:${escapeIcsText(`${booking.appointmentType.name} at ${booking.branch.name}`)}`,
    `LOCATION:${escapeIcsText(location)}`,
    `DESCRIPTION:${escapeIcsText(description)}`,
    'STATUS:CONFIRMED',
    'BEGIN:VALARM',
    'ACTION:DISPLAY',
    'DESCRIPTION:Branch appointment in one hour',
    'TRIGGER:-PT1H',
    'END:VALARM',
    'END:VEVENT',
    'END:VCALENDAR',
  ];

  return lines.map(foldLine).join(CRLF) + CRLF;
}
