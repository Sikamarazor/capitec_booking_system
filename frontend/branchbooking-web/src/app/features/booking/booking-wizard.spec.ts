import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiErrorInterceptor } from '../../core/api/api-error.interceptor';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking, aBranch, aType, anAvailability } from '../../testing/api-fixtures';
import { buttonWithText, click, setValue, textOf } from '../../testing/dom';
import { BookingDraftService } from './booking-draft.service';
import { BookingWizard } from './booking-wizard';

describe('BookingWizard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function start() {
    const fixture = TestBed.createComponent(BookingWizard);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush([aType]);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  async function advanceToBranches(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }, root: HTMLElement) {
    click(root, 'input[type="radio"]');
    click(root, 'button[type="submit"]');
    fixture.detectChanges();
    http.expectOne('/api/v1/branches').flush([aBranch]);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('has one page heading and starts at the first step', async () => {
    const { root } = await start();

    expect(root.querySelectorAll('h1').length).toBe(1);
    expect(textOf(root, 'h1')).toContain('Book an appointment');
    expect(textOf(root, 'bb-stepper')).toContain('Step 1 of 4: Appointment');
    expect(textOf(root, '#step-heading')).toBe('What do you need?');
  });

  it('moves to the branch step after a choice and back again, keeping the choice', async () => {
    const { fixture, root } = await start();

    await advanceToBranches(fixture, root);
    expect(textOf(root, 'bb-stepper')).toContain('Step 2 of 4: Branch');
    expect(textOf(root, '#step-heading')).toBe('Choose a branch');

    buttonWithText(root, 'Back')?.click();
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush([aType]);
    fixture.detectChanges();

    expect(textOf(root, 'bb-stepper')).toContain('Step 1 of 4: Appointment');
    expect(TestBed.inject(BookingDraftService).appointmentType()?.id).toBe('type-1');
  });

  it('walks to the details step', async () => {
    const { fixture, root } = await start();
    await advanceToBranches(fixture, root);

    click(root, 'input[type="radio"]');
    click(root, 'button[type="submit"]');
    fixture.detectChanges();
    TestBed.inject(BookingDraftService).date.set('2026-11-03');
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability').flush(anAvailability);
    fixture.detectChanges();
    await fixture.whenStable();
    click(root, '[data-slot="08:00"]');
    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    expect(textOf(root, 'bb-stepper')).toContain('Step 4 of 4: Your details');
    expect(root.querySelector('#fullName')).toBeTruthy();
  });

  it('returns to the time step with an explanation when the time was just taken', async () => {
    const { fixture, root } = await start();
    const draft = TestBed.inject(BookingDraftService);
    draft.appointmentType.set(aType);
    draft.chooseBranch(aBranch);
    draft.date.set('2026-11-03');
    draft.slot.set(anAvailability.slots[0]);
    draft.step.set(3);
    fixture.detectChanges();

    setValue(root, '#fullName', 'Thandi Nkosi');
    setValue(root, '#email', 'thandi@example.com');
    setValue(root, '#phone', '0821234567');
    click(root, 'button[type="submit"]');
    http.expectOne('/api/v1/bookings').flush({ code: 'slot_not_available' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability').flush(anAvailability);
    fixture.detectChanges();

    expect(textOf(root, 'bb-stepper')).toContain('Step 3 of 4: Time');
    expect(textOf(root)).toContain('That time was just taken');
    expect(draft.slot()).toBeNull();
  });

  it('forgets the draft when the page is left', async () => {
    const { fixture } = await start();
    const draft = TestBed.inject(BookingDraftService);
    draft.appointmentType.set(aType);

    fixture.destroy();

    expect(draft.appointmentType()).toBeNull();
  });

  it('has no accessibility violations', async () => {
    const { root } = await start();

    await expectNoA11yViolations(root);
  });
});
