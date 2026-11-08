import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { apiErrorInterceptor } from '../../../core/api/api-error.interceptor';
import { BookingSessionService } from '../../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../../testing/a11y';
import { aBooking, aBranch, aType, anAvailability } from '../../../testing/api-fixtures';
import { buttonWithText, click, setValue, textOf } from '../../../testing/dom';
import { BookingDraftService } from '../booking-draft.service';
import { DetailsStep, StepNotice } from './details-step';

@Component({ template: 'confirmation placeholder' })
class Placeholder {}

describe('DetailsStep', () => {
  let http: HttpTestingController;
  let draft: BookingDraftService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'confirmation', component: Placeholder }]),
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    draft = TestBed.inject(BookingDraftService);
    draft.appointmentType.set(aType);
    draft.chooseBranch(aBranch);
    draft.slot.set(anAvailability.slots[0]);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(DetailsStep);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  function fillValid(root: HTMLElement): void {
    setValue(root, '#fullName', '  Thandi Nkosi ');
    setValue(root, '#email', 'thandi@example.com');
    setValue(root, '#phone', '082 123 4567');
  }

  it('summarises the choices made so far', () => {
    const { root } = create();

    expect(textOf(root)).toContain('Open an account');
    expect(textOf(root)).toContain('Test Branch, 1 Test Street, Johannesburg');
    expect(textOf(root)).toContain('at 08:00');
    expect(textOf(root)).toContain('Bring your ID document.');
  });

  it('does not call the API when fields are empty, and lists every problem', () => {
    const { fixture, root } = create();

    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    http.expectNone('/api/v1/bookings');
    expect(textOf(root)).toContain('Please fix the following');
    expect(textOf(root)).toContain('Enter your full name.');
    expect(textOf(root)).toContain('Enter your email address.');
    expect(textOf(root)).toContain('Enter your phone number.');
    expect(root.querySelector('#email')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('describes a bad email once the field has been left', () => {
    const { fixture, root } = create();

    setValue(root, '#email', 'not-an-email');
    fixture.detectChanges();

    expect(textOf(root)).toContain('Enter an email address like name@example.com.');
    expect(root.querySelector('#email')?.getAttribute('aria-describedby')).toContain('email-error');
  });

  it('sends the booking with trimmed details and the slot exactly as the API gave it', () => {
    const { fixture, root } = create();
    fillValid(root);

    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    const request = http.expectOne('/api/v1/bookings');
    expect(request.request.body).toEqual({
      branchId: 'branch-1',
      appointmentTypeId: 'type-1',
      startsAtUtc: '2026-11-03T06:00:00+00:00',
      fullName: 'Thandi Nkosi',
      email: 'thandi@example.com',
      phone: '082 123 4567',
    });
    request.flush(aBooking(), { status: 201, statusText: 'Created' });
  });

  it('hands the booking to the session, clears the draft and goes to the confirmation page', async () => {
    const { fixture, root } = create();
    fillValid(root);

    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush(aBooking(), { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    expect(TestBed.inject(BookingSessionService).booking()?.reference).toBe('BK-7H2QX-M9RTD');
    expect(draft.appointmentType()).toBeNull();
    expect(draft.details()).toEqual({ fullName: '', email: '', phone: '' });
    expect(TestBed.inject(Router).url).toBe('/confirmation');
  });

  it('ignores a second click while the first request is in flight', () => {
    const { fixture, root } = create();
    fillValid(root);

    click(root, 'button[type="submit"]');
    fixture.detectChanges();
    click(root, 'button[type="submit"]');

    const requests = http.match('/api/v1/bookings');
    expect(requests.length).toBe(1);
    expect(root.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    expect(textOf(root)).toContain('Confirming...');
    requests[0].flush(aBooking(), { status: 201, statusText: 'Created' });
  });

  it('sends the customer back to the time step when the slot was just taken', () => {
    const { fixture, root } = create();
    let notice: StepNotice | undefined;
    fixture.componentInstance.lost.subscribe((n) => (notice = n));
    fillValid(root);

    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush({ code: 'slot_not_available' }, { status: 409, statusText: 'Conflict' });

    expect(notice?.step).toBe(2);
    expect(notice?.error.title).toBe('That time was just taken');
    expect(draft.slot()).toBeNull();
  });

  it('sends the customer back to the branch step when the branch has gone', () => {
    const { fixture, root } = create();
    let notice: StepNotice | undefined;
    fixture.componentInstance.lost.subscribe((n) => (notice = n));
    fillValid(root);

    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush({ code: 'branch_not_found' }, { status: 404, statusText: 'Not Found' });

    expect(notice?.step).toBe(1);
    expect(draft.branch()).toBeNull();
  });

  it('marks the fields the server rejected with our own wording, not the server text', () => {
    const { fixture, root } = create();
    fillValid(root);

    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush(
      { code: 'validation_failed', errors: { Email: ['server wording thandi@example.com'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(textOf(root)).toContain('Check your email address.');
    expect(textOf(root)).not.toContain('server wording');
    expect(root.querySelector('#email')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('shows a general error with a support code and lets the customer try again', () => {
    const { fixture, root } = create();
    fillValid(root);

    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush({ code: 'internal_error', correlationId: 'abc123' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(textOf(root)).toContain('Something went wrong');
    expect(textOf(root)).toContain('Support code: abc123');
    expect(root.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(false);
  });

  it('keeps what was typed when the customer goes back', () => {
    const { fixture, root } = create();
    let back = 0;
    fixture.componentInstance.back.subscribe(() => back++);

    setValue(root, '#fullName', 'Thandi Nkosi');
    buttonWithText(root, 'Back')?.click();

    expect(back).toBe(1);
    expect(draft.details().fullName).toBe('Thandi Nkosi');
  });

  it('has no accessibility violations', async () => {
    const { root } = create();

    await expectNoA11yViolations(root);
  });
});
