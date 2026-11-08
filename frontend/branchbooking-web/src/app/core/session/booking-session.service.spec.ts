import { TestBed } from '@angular/core/testing';
import { aBooking } from '../../testing/api-fixtures';
import { BookingSessionService } from './booking-session.service';

describe('BookingSessionService', () => {
  it('starts empty, holds a booking and can be cleared', () => {
    const session = TestBed.inject(BookingSessionService);
    expect(session.booking()).toBeNull();

    session.set(aBooking());
    expect(session.booking()?.reference).toBe('BK-7H2QX-M9RTD');

    session.clear();
    expect(session.booking()).toBeNull();
  });

  it('never writes the booking to browser storage', () => {
    const session = TestBed.inject(BookingSessionService);

    session.set(aBooking());

    expect(JSON.stringify({ ...localStorage })).not.toContain('BK-7H2QX');
    expect(JSON.stringify({ ...sessionStorage })).not.toContain('BK-7H2QX');
  });
});
