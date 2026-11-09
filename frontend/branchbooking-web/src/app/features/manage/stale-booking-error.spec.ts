import { ApiError } from '../../core/api/api-error';
import { isStaleBookingError } from './stale-booking-error';

describe('isStaleBookingError', () => {
  it('is true for the codes that say the booking changed', () => {
    for (const code of ['booking_not_active', 'change_window_closed', 'reschedule_limit_reached', 'concurrent_change']) {
      expect(isStaleBookingError(new ApiError(409, code))).toBe(true);
    }
  });

  it('is false for slot problems, network problems and non-API errors', () => {
    expect(isStaleBookingError(new ApiError(409, 'slot_not_available'))).toBe(false);
    expect(isStaleBookingError(new ApiError(0, 'network_error'))).toBe(false);
    expect(isStaleBookingError(new Error('boom'))).toBe(false);
  });
});
