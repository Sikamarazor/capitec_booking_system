import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { BookingMessage } from '../../core/api/api.models';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aMessage } from '../../testing/api-fixtures';
import { click, textOf } from '../../testing/dom';
import { BookingMessages } from './booking-messages';

const URL = '/api/v1/bookings/BK-7H2QX-M9RTD/messages';

describe('BookingMessages', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(messages: BookingMessage[] | 'error' = [aMessage()]) {
    const fixture = TestBed.createComponent(BookingMessages);
    fixture.componentRef.setInput('reference', 'BK-7H2QX-M9RTD');
    fixture.componentRef.setInput('timeZoneId', 'Africa/Johannesburg');
    fixture.detectChanges();
    const request = http.expectOne(URL);
    if (messages === 'error') {
      request.error(new ProgressEvent('error'));
    } else {
      request.flush(messages);
    }
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('lists messages with friendly names, status and the time on the branch clock', async () => {
    const { root } = await render([
      aMessage(),
      aMessage({ kind: 'Reminder', status: 'Pending', sentAtUtc: null, subject: null, body: null, channel: null }),
    ]);

    expect(textOf(root)).toContain('Booking confirmation');
    expect(textOf(root)).toContain('Sent');
    expect(textOf(root)).toContain('at 09:00');
    expect(textOf(root)).toContain('Reminder');
    expect(textOf(root)).toContain('Waiting to send');
  });

  it('labels a simulated message as a demo and lets the customer read it', async () => {
    const { root } = await render();

    expect(textOf(root)).toContain('Demo message. Nothing was emailed.');
    expect(textOf(root, 'details summary')).toBe('Read the message');
    expect(textOf(root, 'details pre')).toContain('Your appointment is confirmed.');
  });

  it('shows no demo label and no body for a real channel', async () => {
    const { root } = await render([aMessage({ channel: 'Smtp', subject: null, body: null })]);

    expect(textOf(root)).not.toContain('Demo message');
    expect(root.querySelector('details')).toBeNull();
  });

  it('says when there are no messages yet', async () => {
    const { root } = await render([]);

    expect(textOf(root)).toContain('No messages yet.');
  });

  it('loads again when Refresh is pressed', async () => {
    const { fixture, root } = await render();

    click(root, 'button.btn-secondary');
    http.expectOne(URL).flush([aMessage(), aMessage({ kind: 'Reminder', status: 'Sent' })]);
    fixture.detectChanges();

    expect(root.querySelectorAll('li').length).toBe(2);
  });

  it('loads again when the refresh input changes', async () => {
    const { fixture } = await render();

    fixture.componentRef.setInput('refresh', 1);
    fixture.detectChanges();

    http.expectOne(URL).flush([]);
  });

  it('shows a friendly error and keeps the retry button', async () => {
    const { root } = await render('error');

    expect(textOf(root)).toContain('No connection');
    expect(root.querySelector('button.btn-secondary')).toBeTruthy();
  });

  it('has no accessibility violations', async () => {
    const { root } = await render();

    await expectNoA11yViolations(root);
  });
});
