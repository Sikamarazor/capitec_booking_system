import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { aBooking } from '../../testing/api-fixtures';
import { BranchBookingApiService } from './branch-booking-api.service';

describe('BranchBookingApiService', () => {
  let api: BranchBookingApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(BranchBookingApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists appointment types', () => {
    api.getAppointmentTypes().subscribe();

    const request = http.expectOne('/api/v1/appointment-types');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('lists branches', () => {
    api.getBranches().subscribe();

    http.expectOne('/api/v1/branches').flush([]);
  });

  it('asks for availability with the date as a query parameter', () => {
    api.getAvailability('branch-1', '2026-11-03').subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability');
    expect(request.request.params.get('date')).toBe('2026-11-03');
    request.flush({});
  });

  it('creates a booking with the request as the body', () => {
    const body = {
      branchId: 'b',
      appointmentTypeId: 't',
      startsAtUtc: '2026-11-03T06:00:00+00:00',
      fullName: 'Thandi Nkosi',
      email: 'thandi@example.com',
      phone: '0821234567',
    };
    api.createBooking(body).subscribe();

    const request = http.expectOne('/api/v1/bookings');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(aBooking());
  });

  it('looks a booking up by reference', () => {
    api.getBooking('BK-7H2QX-M9RTD').subscribe();

    const request = http.expectOne('/api/v1/bookings/BK-7H2QX-M9RTD');
    expect(request.request.method).toBe('GET');
    request.flush(aBooking());
  });

  it('cancels with a trimmed reason, or an empty body when there is none', () => {
    api.cancelBooking('BK-7H2QX-M9RTD', '  Plans changed  ').subscribe();
    const withReason = http.expectOne('/api/v1/bookings/BK-7H2QX-M9RTD/cancel');
    expect(withReason.request.body).toEqual({ reason: 'Plans changed' });
    withReason.flush(aBooking());

    api.cancelBooking('BK-7H2QX-M9RTD', '   ').subscribe();
    const withoutReason = http.expectOne('/api/v1/bookings/BK-7H2QX-M9RTD/cancel');
    expect(withoutReason.request.body).toEqual({});
    withoutReason.flush(aBooking());
  });

  it('reschedules with the new start exactly as given', () => {
    api.rescheduleBooking('BK-7H2QX-M9RTD', '2026-11-04T06:00:00+00:00').subscribe();

    const request = http.expectOne('/api/v1/bookings/BK-7H2QX-M9RTD/reschedule');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ newStartsAtUtc: '2026-11-04T06:00:00+00:00' });
    request.flush(aBooking());
  });

  it('reads the messages for a booking', () => {
    api.getMessages('BK-7H2QX-M9RTD').subscribe();

    http.expectOne('/api/v1/bookings/BK-7H2QX-M9RTD/messages').flush([]);
  });

  it('encodes anything unusual in a reference rather than building a different URL', () => {
    api.getBooking('a/b?c').subscribe();

    http.expectOne('/api/v1/bookings/a%2Fb%3Fc').flush(aBooking());
  });
});
