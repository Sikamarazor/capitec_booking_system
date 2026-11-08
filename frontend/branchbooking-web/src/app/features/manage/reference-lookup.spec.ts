import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking } from '../../testing/api-fixtures';
import { click, setValue, textOf } from '../../testing/dom';
import { ReferenceLookup } from './reference-lookup';

const URL = '/api/v1/bookings/BK-7H2QX-M9RTD';

describe('ReferenceLookup', () => {
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
    const fixture = TestBed.createComponent(ReferenceLookup);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  function lookUp(fixture: { detectChanges(): void }, root: HTMLElement, text: string): void {
    setValue(root, '#reference', text);
    click(root, 'button[type="submit"]');
    fixture.detectChanges();
  }

  it('refuses an empty or malformed reference without sending a request', () => {
    const { fixture, root } = render();

    lookUp(fixture, root, '');
    expect(textOf(root)).toContain('Enter your booking reference.');

    lookUp(fixture, root, 'not a reference');
    expect(textOf(root)).toContain('Enter your reference like BK-7H2QX-M9RTD.');
    expect(root.querySelector('#reference')?.getAttribute('aria-invalid')).toBe('true');
    expect(session.booking()).toBeNull();
  });

  it('tidies the reference, looks it up, stores the booking and clears the box', () => {
    const { fixture, root } = render();
    let found = 0;
    fixture.componentInstance.found.subscribe(() => found++);

    lookUp(fixture, root, ' bk 7h2qx m9rtd ');
    const request = http.expectOne(URL);
    expect(request.request.method).toBe('GET');
    request.flush(aBooking());
    fixture.detectChanges();

    expect(session.booking()?.reference).toBe('BK-7H2QX-M9RTD');
    expect((root.querySelector('#reference') as HTMLInputElement).value).toBe('');
    expect(found).toBe(1);
  });

  it('shows our own words when the booking is not found, and keeps what was typed', () => {
    const { fixture, root } = render();

    lookUp(fixture, root, 'BK-7H2QX-M9RTD');
    http
      .expectOne(URL)
      .flush({ code: 'booking_not_found', detail: 'server wording', correlationId: 'abc123' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(textOf(root)).toContain('We could not find that booking');
    expect(textOf(root)).toContain('Support code: abc123');
    expect(textOf(root)).not.toContain('server wording');
    expect(session.booking()).toBeNull();
    expect((root.querySelector('#reference') as HTMLInputElement).value).toBe('BK-7H2QX-M9RTD');
  });

  it('tells the customer how long to wait when rate limited', () => {
    const { fixture, root } = render();

    lookUp(fixture, root, 'BK-7H2QX-M9RTD');
    http
      .expectOne(URL)
      .flush({ code: 'rate_limited' }, { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '12' } });
    fixture.detectChanges();

    expect(textOf(root)).toContain('Please wait 12 seconds and try again.');
  });

  it('reports a lost connection and works again on retry', () => {
    const { fixture, root } = render();

    lookUp(fixture, root, 'BK-7H2QX-M9RTD');
    http.expectOne(URL).error(new ProgressEvent('error'));
    fixture.detectChanges();
    expect(textOf(root)).toContain('No connection');

    click(root, 'button[type="submit"]');
    http.expectOne(URL).flush(aBooking());
    expect(session.booking()).toBeTruthy();
  });

  it('sends one request however many times the button is pressed', () => {
    const { fixture, root } = render();

    setValue(root, '#reference', 'BK-7H2QX-M9RTD');
    click(root, 'button[type="submit"]');
    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    expect(textOf(root, 'button[type="submit"]')).toContain('Looking...');
    http.expectOne(URL).flush(aBooking());
    fixture.detectChanges();
    expect(textOf(root, 'button[type="submit"]')).toContain('Find my booking');
  });

  it('has no accessibility violations, including with an error showing', async () => {
    const { fixture, root } = render();
    await expectNoA11yViolations(root);

    lookUp(fixture, root, 'nope');
    await expectNoA11yViolations(root);
  });
});
