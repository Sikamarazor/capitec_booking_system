import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { ErrorDescription } from '../../core/api/error-messages';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking } from '../../testing/api-fixtures';
import { buttonWithText, click, setValue, textOf } from '../../testing/dom';
import { CancelBooking } from './cancel-booking';

const CANCEL_URL = '/api/v1/bookings/BK-7H2QX-M9RTD/cancel';

describe('CancelBooking', () => {
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

  function render() {
    const fixture = TestBed.createComponent(CancelBooking);
    fixture.componentRef.setInput('booking', aBooking());
    fixture.detectChanges();
    const events = { done: 0, dismissed: 0, stale: [] as ErrorDescription[] };
    fixture.componentInstance.done.subscribe(() => events.done++);
    fixture.componentInstance.dismissed.subscribe(() => events.dismissed++);
    fixture.componentInstance.stale.subscribe((problem) => events.stale.push(problem));
    return { fixture, root: fixture.nativeElement as HTMLElement, events };
  }

  it('names the appointment, takes focus and sends nothing yet', async () => {
    const { fixture, root } = render();
    await fixture.whenStable();

    expect(textOf(root, 'h2')).toBe('Cancel this appointment?');
    expect(textOf(root)).toContain('Open an account at Test Branch');
    expect(textOf(root)).toContain('at 08:00');
    expect(document.activeElement?.tagName).toBe('H2');
  });

  it('cancels with the trimmed reason, stores the updated booking and says it is done', () => {
    const { fixture, root, events } = render();

    setValue(root, '#cancel-reason', '  Plans changed ');
    click(root, 'button[type="submit"]');
    const request = http.expectOne(CANCEL_URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Plans changed' });
    request.flush(aBooking({ status: 'Cancelled', canCancel: false, canReschedule: false }));
    fixture.detectChanges();

    expect(session.booking()?.status).toBe('Cancelled');
    expect(events.done).toBe(1);
  });

  it('sends an empty body when there is no reason', () => {
    const { root } = render();

    click(root, 'button[type="submit"]');

    const request = http.expectOne(CANCEL_URL);
    expect(request.request.body).toEqual({});
    request.flush(aBooking({ status: 'Cancelled' }));
  });

  it('keeps the appointment without any request when the customer changes their mind', () => {
    const { root, events } = render();

    buttonWithText(root, 'Keep my appointment')?.click();

    expect(events.dismissed).toBe(1);
    expect(session.booking()).toBeNull();
  });

  it('shows a friendly error in place, keeps the reason and allows another try', () => {
    const { fixture, root, events } = render();

    setValue(root, '#cancel-reason', 'Plans changed');
    click(root, 'button[type="submit"]');
    http.expectOne(CANCEL_URL).error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(textOf(root)).toContain('No connection');
    expect((root.querySelector('#cancel-reason') as HTMLTextAreaElement).value).toBe('Plans changed');
    expect(events.stale.length).toBe(0);

    click(root, 'button[type="submit"]');
    http.expectOne(CANCEL_URL).flush(aBooking({ status: 'Cancelled' }));
    expect(events.done).toBe(1);
  });

  it('hands the page a stale-booking refusal instead of showing it inline', () => {
    const { fixture, root, events } = render();

    click(root, 'button[type="submit"]');
    http.expectOne(CANCEL_URL).flush({ code: 'change_window_closed' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(events.stale.length).toBe(1);
    expect(events.stale[0].title).toBe('Too late to change this online');
    expect(events.done).toBe(0);
    expect(root.querySelector('[role="alert"]')).toBeNull();
  });

  it('sends one request however many times the button is pressed', () => {
    const { fixture, root } = render();

    click(root, 'button[type="submit"]');
    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    expect(textOf(root, 'button[type="submit"]')).toContain('Cancelling...');
    http.expectOne(CANCEL_URL).flush(aBooking({ status: 'Cancelled' }));
  });

  it('has no accessibility violations', async () => {
    const { root } = render();

    await expectNoA11yViolations(root);
  });
});
