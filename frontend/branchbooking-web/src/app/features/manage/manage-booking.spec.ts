import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking, aMessage } from '../../testing/api-fixtures';
import { buttonWithText, click, setValue, textOf } from '../../testing/dom';
import { ManageBooking } from './manage-booking';

const BOOKING_URL = '/api/v1/bookings/BK-7H2QX-M9RTD';

describe('ManageBooking', () => {
  let http: HttpTestingController;
  let session: BookingSessionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(BookingSessionService);
  });

  afterEach(() => http.verify());

  function render() {
    const fixture = TestBed.createComponent(ManageBooking);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('asks for the reference when no booking is in memory', () => {
    const { root } = render();

    expect(textOf(root, 'h1')).toBe('Manage your booking');
    expect(textOf(root)).toContain('Find your booking');
    expect(root.querySelector('#reference')).toBeTruthy();
  });

  it('shows the booking at once when the customer has just booked, with its messages', async () => {
    session.set(aBooking());
    const { fixture, root } = render();

    http.expectOne(`${BOOKING_URL}/messages`).flush([aMessage()]);
    await settle(fixture);

    expect(textOf(root)).toContain('Your appointment');
    expect(textOf(root)).toContain('t*****@example.com');
    expect(textOf(root)).toContain('Booking confirmation');
    expect(root.querySelector('#reference')).toBeNull();
  });

  it('shows the booking after a lookup and moves focus to its heading', async () => {
    const { fixture, root } = render();

    setValue(root, '#reference', 'BK-7H2QX-M9RTD');
    click(root, 'button[type="submit"]');
    http.expectOne(BOOKING_URL).flush(aBooking());
    fixture.detectChanges();
    http.expectOne(`${BOOKING_URL}/messages`).flush([]);
    await settle(fixture);

    expect(textOf(root, '#details-heading')).toBe('Your appointment');
    expect(document.activeElement?.id).toBe('details-heading');
  });

  it('clears the booking from memory and goes back to the lookup', async () => {
    session.set(aBooking());
    const { fixture, root } = render();
    http.expectOne(`${BOOKING_URL}/messages`).flush([]);
    await settle(fixture);

    buttonWithText(root, 'Look up a different booking')?.click();
    await settle(fixture);

    expect(session.booking()).toBeNull();
    expect(root.querySelector('#reference')).toBeTruthy();
    expect(document.activeElement?.id).toBe('lookup-heading');
  });

  it('says plainly that a cancelled booking cannot be changed and offers a new booking', async () => {
    session.set(aBooking({ status: 'Cancelled', canCancel: false, canReschedule: false, cancelledAtUtc: '2026-11-02T09:00:00+00:00' }));
    const { fixture, root } = render();
    http.expectOne(`${BOOKING_URL}/messages`).flush([]);
    await settle(fixture);

    expect(textOf(root)).toContain('This booking is cancelled');
    expect(root.querySelector('a[href="/book"]')).toBeTruthy();
  });

  it('keeps the reference out of the address bar, the title and browser storage', async () => {
    const storage = [localStorage.length, sessionStorage.length];
    const { fixture, root } = render();

    setValue(root, '#reference', 'BK-7H2QX-M9RTD');
    click(root, 'button[type="submit"]');
    http.expectOne(BOOKING_URL).flush(aBooking());
    fixture.detectChanges();
    http.expectOne(`${BOOKING_URL}/messages`).flush([]);
    await settle(fixture);

    expect(location.href).not.toContain('BK-7H2QX');
    expect(document.title).not.toContain('BK-7H2QX');
    expect([localStorage.length, sessionStorage.length]).toEqual(storage);
  });

  it('has no accessibility violations in the lookup state', async () => {
    const { root } = render();

    await expectNoA11yViolations(root);
  });

  it('has no accessibility violations in the booking state', async () => {
    session.set(aBooking());
    const { fixture, root } = render();
    http.expectOne(`${BOOKING_URL}/messages`).flush([aMessage()]);
    await settle(fixture);

    await expectNoA11yViolations(root);
  });
});
