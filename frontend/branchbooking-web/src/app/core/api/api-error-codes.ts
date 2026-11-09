export const API_ERROR_CODES = [
  'validation_failed',
  'invalid_customer_details',
  'invalid_cancellation_reason',
  'date_outside_booking_window',
  'slot_not_offered',
  'same_slot',
  'branch_not_found',
  'appointment_type_not_found',
  'booking_not_found',
  'slot_not_available',
  'booking_not_active',
  'concurrent_change',
  'slot_too_soon',
  'change_window_closed',
  'reschedule_limit_reached',
  'rate_limited',
  'internal_error',
] as const;

/** Codes the client invents when there is no usable problem response. */
export const CLIENT_ERROR_CODES = ['network_error', 'unexpected_error'] as const;

export type ApiErrorCode = (typeof API_ERROR_CODES)[number];
export type ClientErrorCode = (typeof CLIENT_ERROR_CODES)[number];
