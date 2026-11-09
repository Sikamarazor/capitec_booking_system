import { ApiError } from '../../core/api/api-error';

/** Refusals that mean the booking is no longer as the screen shows it. The page loads it again. */
const STALE_CODES: readonly string[] = [
  'booking_not_active',
  'change_window_closed',
  'reschedule_limit_reached',
  'concurrent_change',
];

export function isStaleBookingError(error: unknown): boolean {
  return error instanceof ApiError && STALE_CODES.includes(error.code);
}
