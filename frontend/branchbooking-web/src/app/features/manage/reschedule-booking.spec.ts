import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { ErrorDescription } from '../../core/api/error-messages';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking, anAvailability } from '../../testing/api-fixtures';
import { buttonWithText, click, textOf } from '../../testing/dom';
import { RescheduleBooking } from './reschedule-booking';

const AVAILABILITY_URL = '/api/v1/branches/branch-1/availability';
const RESCHEDULE_URL = '/api/v1/bookings/BK-7H2QX-M9RTD/reschedule';

describe('RescheduleBooking', () => {
  let http: HttpTestingController;
  let session: BookingSessionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(BookingSessionService);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(RescheduleBooking);
    fixture.componentRef.setInput('booking', aBooking());
    fixture.detectChanges();
    const events = { done: 0, dismissed: 0, stale: [] as ErrorDescription[] };
    fixture.componentInstance.done.subscribe(() => events.done++);
    fixture.componentInstance.dismissed.subscribe(() => events.dismissed++);
    fixture.componentInstance.stale.subscribe((problem) => events.stale.push(problem));

    const request = http.expectOne((r) => r.url === AVAILABILITY_URL);
    expect(request.request.params.get('date')).toBe('2026-11-03');
    request.flush(anAvailability);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, root: fixture.nativeElement as HTMLElement, events };
  }

  function pickNine(fixture: ComponentFixture<RescheduleBooking>, root: HTMLElement): void {
    click(root, '[data-slot="09:00"]');
    fixture.detectChanges();
  }

  it("opens on the appointment's own day, shows the current time as taken and takes focus", async () => {
    const { root } = await render();

    expect(textOf(root, 'h2')).toBe('Move your appointment');
    expect(textOf(root)).toContain('Your appointment is now');
    expect((root.querySelector('[data-slot="08:00"]') as HTMLButtonElement).disabled).toBe(true);
    expect(textOf(root, '[data-slot="08:00"]')).toContain('Current time');
    expect(document.activeElement?.tagName).toBe('H2');
  });

  it('asks for a time first, then says what will change, on the branch clock', async () => {
    const { fixture, root } = await render();
    expect((buttonWithText(root, 'Confirm new time') as HTMLButtonElement).disabled).toBe(true);

    pickNine(fixture, root);

    expect(textOf(root)).toContain('Move your appointment to');
    expect(textOf(root, '[role="status"]')).toContain('at 09:00');
    expect((buttonWithText(root, 'Confirm new time') as HTMLButtonElement).disabled).toBe(false);
  });

  it('sends the new start exactly as the API gave it, stores the updated booking and says it is done', async () => {
    const { fixture, root, events } = await render();
    pickNine(fixture, root);

    buttonWithText(root, 'Confirm new time')?.click();
    const request = http.expectOne(RESCHEDULE_URL);
    expect(request.request.body).toEqual({ newStartsAtUtc: '2026-11-03T07:00:00+00:00' });
    request.flush(aBooking({ startsAtUtc: '2026-11-03T07:00:00+00:00', endsAtUtc: '2026-11-03T07:30:00+00:00', rescheduleCount: 1 }));
    fixture.detectChanges();

    expect(session.booking()?.rescheduleCount).toBe(1);
    expect(events.done).toBe(1);
  });

  it('stays, clears the choice and loads the day again when the time was just taken', async () => {
    const { fixture, root, events } = await render();
    pickNine(fixture, root);

    buttonWithText(root, 'Confirm new time')?.click();
    http.expectOne(RESCHEDULE_URL).flush({ code: 'slot_not_available' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(textOf(root)).toContain('That time was just taken');
    expect(textOf(root)).not.toContain('Move your appointment to');
    expect(events.done).toBe(0);
    expect(events.stale.length).toBe(0);

    const takenNow = {
      ...anAvailability,
      slots: anAvailability.slots.map((s) => (s.localStart === '09:00' ? { ...s, isAvailable: false, remaining: 0 } : s)),
    };
    http.expectOne((r) => r.url === AVAILABILITY_URL).flush(takenNow);
    fixture.detectChanges();
    expect((root.querySelector('[data-slot="09:00"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('hands the page a stale-booking refusal instead of showing it inline', async () => {
    const { fixture, root, events } = await render();
    pickNine(fixture, root);

    buttonWithText(root, 'Confirm new time')?.click();
    http.expectOne(RESCHEDULE_URL).flush({ code: 'reschedule_limit_reached' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(events.stale.length).toBe(1);
    expect(events.stale[0].title).toBe('This booking cannot be moved again');
    expect(root.querySelector('[role="alert"]')).toBeNull();
  });

  it('keeps the chosen time when the connection fails so the customer can try again', async () => {
    const { fixture, root, events } = await render();
    pickNine(fixture, root);

    buttonWithText(root, 'Confirm new time')?.click();
    http.expectOne(RESCHEDULE_URL).error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(textOf(root)).toContain('No connection');
    expect(textOf(root)).toContain('Move your appointment to');

    buttonWithText(root, 'Confirm new time')?.click();
    http.expectOne(RESCHEDULE_URL).flush(aBooking({ rescheduleCount: 1 }));
    expect(events.done).toBe(1);
  });

  it('keeps the current time without any request when the customer changes their mind', async () => {
    const { root, events } = await render();

    buttonWithText(root, 'Keep my current time')?.click();

    expect(events.dismissed).toBe(1);
  });

  it('sends one request however many times the button is pressed', async () => {
    const { fixture, root } = await render();
    pickNine(fixture, root);

    buttonWithText(root, 'Confirm new time')?.click();
    fixture.detectChanges();
    buttonWithText(root, 'Moving...')?.click();

    http.expectOne(RESCHEDULE_URL).flush(aBooking({ rescheduleCount: 1 }));
  });

  it('has no accessibility violations', async () => {
    const { fixture, root } = await render();
    pickNine(fixture, root);

    await expectNoA11yViolations(root);
  });
});
