import { ApiError } from './api-error';
import { ApiErrorCode, ClientErrorCode } from './api-error-codes';

export interface ErrorDescription {
  title: string;
  message: string;
  /** True when trying the same thing again may work. */
  retryable: boolean;
  /** The correlation id, to quote when contacting support. */
  supportCode: string | null;
}

type Entry = Omit<ErrorDescription, 'supportCode'>;

const GENERIC: Entry = {
  title: 'Something went wrong',
  message: 'We could not complete that. Please try again, and contact us if it keeps happening.',
  retryable: true,
};

/** Customer-facing copy for every code. Never shows the server's wording. */
export const ERROR_MESSAGES: Readonly<Record<ApiErrorCode | ClientErrorCode, Entry>> = {
  validation_failed: {
    title: 'Please check your details',
    message: 'Some of the information you entered is not valid. Check the highlighted fields and try again.',
    retryable: true,
  },
  invalid_customer_details: {
    title: 'Please check your details',
    message: 'Check your name, email address and phone number, then try again.',
    retryable: true,
  },
  invalid_cancellation_reason: {
    title: 'Reason is too long',
    message: 'Please keep your reason to 500 characters or fewer.',
    retryable: true,
  },
  date_outside_booking_window: {
    title: 'That date is not available',
    message: 'We are not taking bookings for that date. Choose a nearer date.',
    retryable: false,
  },
  slot_not_offered: {
    title: 'That time is not offered',
    message: 'The branch does not offer that time. Choose another time.',
    retryable: false,
  },
  same_slot: {
    title: 'That is the time you already have',
    message: 'Choose a different time to move your appointment.',
    retryable: false,
  },
  branch_not_found: {
    title: 'That branch is not available',
    message: 'Choose another branch.',
    retryable: false,
  },
  appointment_type_not_found: {
    title: 'That appointment type is not available',
    message: 'Choose another appointment type.',
    retryable: false,
  },
  booking_not_found: {
    title: 'We could not find that booking',
    message: 'Check the reference and try again. It looks like BK-7H2QX-M9RTD.',
    retryable: true,
  },
  slot_not_available: {
    title: 'That time was just taken',
    message: 'Someone else booked the last place at that time. Please choose another time.',
    retryable: false,
  },
  booking_not_active: {
    title: 'This booking is already cancelled',
    message: 'A cancelled booking cannot be changed. You can make a new booking.',
    retryable: false,
  },
  concurrent_change: {
    title: 'Please try again',
    message: 'This booking changed while you were working on it. Check the details and try again.',
    retryable: true,
  },
  slot_too_soon: {
    title: 'That time is too soon',
    message: 'That time is too close to book online. Choose a later time.',
    retryable: false,
  },
  change_window_closed: {
    title: 'Too late to change this online',
    message: 'This appointment is too close to change online. Please contact your branch.',
    retryable: false,
  },
  reschedule_limit_reached: {
    title: 'This booking cannot be moved again',
    message: 'You can cancel it and make a new booking instead.',
    retryable: false,
  },
  rate_limited: {
    title: 'Too many attempts',
    message: 'Please wait a moment and try again.',
    retryable: true,
  },
  internal_error: GENERIC,
  network_error: {
    title: 'No connection',
    message: 'We could not reach the booking service. Check your connection and try again.',
    retryable: true,
  },
  unexpected_error: GENERIC,
};

export function describeError(error: unknown): ErrorDescription {
  if (!(error instanceof ApiError)) {
    return { ...GENERIC, supportCode: null };
  }

  const known = (ERROR_MESSAGES as Readonly<Record<string, Entry>>)[error.code] ?? GENERIC;
  const message =
    error.code === 'rate_limited' && error.retryAfterSeconds !== null
      ? `Please wait ${error.retryAfterSeconds} ${error.retryAfterSeconds === 1 ? 'second' : 'seconds'} and try again.`
      : known.message;

  return { ...known, message, supportCode: error.correlationId };
}
