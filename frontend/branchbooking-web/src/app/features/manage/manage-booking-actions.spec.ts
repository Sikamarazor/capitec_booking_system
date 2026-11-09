import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { Booking } from '../../core/api/api.models';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking, aMessage, anAvailability } from '../../testing/api-fixtures';
import { buttonWithText, click, textOf } from '../../testing/dom';
import { ManageBooking } from './manage-booking';

const BOOKING_URL = '/api/v1/bookings/BK-7H2QX-M9RTD';
const MESSAGES_URL = `${BOOKING_URL}/messages`;

describe('ManageBooking actions', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function open(booking: Booking = aBooking()) {
    TestBed.inject(BookingSessionService).set(booking);
    const fixture = TestBed.createComponent(ManageBooking);
    fixture.detectChanges();
    http.expectOne(MESSAGES_URL).flush([aMessage()]);
    await settle(fixture);
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('offers both actions when the API allows both', async () => {
    const { root } = await open();

    expect(buttonWithText(root, 'Move appointment')).toBeTruthy();
    expect(buttonWithText(root, 'Cancel appointment')).toBeTruthy();
  });

  it('offers neither when the API allows neither, and says to contact the branch', async () => {
    const { root } = await open(aBooking({ canCancel: false, canReschedule: false }));

    expect(buttonWithText(root, 'Move appointment')).toBeNull();
    expect(buttonWithText(root, 'Cancel appointment')).toBeNull();
    expect(textOf(root)).toContain('can no longer be changed online. Please contact your branch.');
  });

  it('offers only cancel, and explains, when the booking cannot be moved', async () => {
    const { root } = await open(aBooking({ canReschedule: false }));

    expect(buttonWithText(root, 'Move appointment')).toBeNull();
    expect(buttonWithText(root, 'Cancel appointment')).toBeTruthy();
    expect(textOf(root)).toContain('cannot be moved online');
  });

  it('opens the cancel panel and returns to the booking, focused, when the customer keeps it', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Cancel appointment')?.click();
    await settle(fixture);
    expect(textOf(root)).toContain('Cancel this appointment?');
    expect(buttonWithText(root, 'Move appointment')).toBeNull();

    buttonWithText(root, 'Keep my appointment')?.click();
    await settle(fixture);

    expect(textOf(root)).not.toContain('Cancel this appointment?');
    expect(document.activeElement?.id).toBe('details-heading');
  });

  it('cancels, shows the cancelled booking with a notice, and reloads the messages', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Cancel appointment')?.click();
    await settle(fixture);
    buttonWithText(root, 'Yes, cancel appointment')?.click();
    http.expectOne(`${BOOKING_URL}/cancel`).flush(
      aBooking({ status: 'Cancelled', canCancel: false, canReschedule: false, cancelledAtUtc: '2026-11-02T09:00:00+00:00' }),
    );
    fixture.detectChanges();
    http.expectOne(MESSAGES_URL).flush([aMessage(), aMessage({ kind: 'Cancellation', status: 'Pending', sentAtUtc: null, subject: null, body: null, channel: null })]);
    await settle(fixture);

    expect(textOf(root)).toContain('Appointment cancelled');
    expect(textOf(root)).toContain('This booking is cancelled');
    expect(buttonWithText(root, 'Cancel appointment')).toBeNull();
    expect(textOf(root)).toContain('Cancellation');
    expect(document.activeElement?.id).toBe('details-heading');
  });

  it('moves the appointment, shows the new time with a notice, and reloads the messages', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Move appointment')?.click();
    await settle(fixture);
    http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability').flush(anAvailability);
    await settle(fixture);
    click(root, '[data-slot="09:00"]');
    fixture.detectChanges();
    buttonWithText(root, 'Confirm new time')?.click();
    http.expectOne(`${BOOKING_URL}/reschedule`).flush(
      aBooking({ startsAtUtc: '2026-11-03T07:00:00+00:00', endsAtUtc: '2026-11-03T07:30:00+00:00', startsAtLocal: '2026-11-03T09:00:00+02:00', rescheduleCount: 1 }),
    );
    fixture.detectChanges();
    http.expectOne(MESSAGES_URL).flush([aMessage()]);
    await settle(fixture);

    expect(textOf(root)).toContain('Appointment moved');
    expect(textOf(root)).toContain('09:00 to 09:30');
    expect(textOf(root)).not.toContain('Move your appointment');
  });

  it('loads the booking again and explains when the server says it is too late to change', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Cancel appointment')?.click();
    await settle(fixture);
    buttonWithText(root, 'Yes, cancel appointment')?.click();
    http.expectOne(`${BOOKING_URL}/cancel`).flush({ code: 'change_window_closed' }, { status: 409, statusText: 'Conflict' });
    http.expectOne(BOOKING_URL).flush(aBooking({ canCancel: false, canReschedule: false }));
    fixture.detectChanges();
    http.expectOne(MESSAGES_URL).flush([aMessage()]);
    await settle(fixture);

    expect(textOf(root)).toContain('Too late to change this online');
    expect(buttonWithText(root, 'Cancel appointment')).toBeNull();
    expect(textOf(root)).not.toContain('Cancel this appointment?');
  });

  it('has no accessibility violations with the cancel panel open', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Cancel appointment')?.click();
    await settle(fixture);

    await expectNoA11yViolations(root);
  });

  it('has no accessibility violations with the reschedule panel open', async () => {
    const { fixture, root } = await open();

    buttonWithText(root, 'Move appointment')?.click();
    await settle(fixture);
    http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability').flush(anAvailability);
    await settle(fixture);

    await expectNoA11yViolations(root);
  });
});
