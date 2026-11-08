import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Slot } from '../../core/api/api.models';
import { addDays, todayIn } from '../../core/time/branch-time';
import { expectNoA11yViolations } from '../../testing/a11y';
import { anAvailability } from '../../testing/api-fixtures';
import { click, textOf } from '../../testing/dom';
import { SlotPicker } from './slot-picker';

describe('SlotPicker', () => {
  const zone = 'Africa/Johannesburg';
  const url = '/api/v1/branches/branch-1/availability';
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create(inputs: Record<string, unknown> = {}): ComponentFixture<SlotPicker> {
    const fixture = TestBed.createComponent(SlotPicker);
    fixture.componentRef.setInput('branchId', 'branch-1');
    fixture.componentRef.setInput('timeZoneId', zone);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return fixture;
  }

  async function settle(fixture: ComponentFixture<SlotPicker>): Promise<HTMLElement> {
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('asks for nothing until a day is chosen', () => {
    const fixture = create();

    http.expectNone((r) => r.url === url);
    expect(textOf(fixture.nativeElement as HTMLElement)).toContain('Choose a day');
  });

  it('loads the chosen date and lists its times', async () => {
    const fixture = create({ date: '2026-11-03' });

    const request = http.expectOne((r) => r.url === url);
    expect(request.request.params.get('date')).toBe('2026-11-03');
    request.flush(anAvailability);
    const root = await settle(fixture);

    expect(root.querySelector('[data-slot="08:00"]')).toBeTruthy();
    expect(root.querySelector('[data-slot="09:00"]')).toBeTruthy();
  });

  it('disables full times and says so, and says when only one place is left', async () => {
    const fixture = create({ date: '2026-11-03' });
    http.expectOne((r) => r.url === url).flush(anAvailability);
    const root = await settle(fixture);

    const full = root.querySelector<HTMLButtonElement>('[data-slot="08:30"]');
    expect(full?.disabled).toBe(true);
    expect(full?.textContent).toContain('Full');
    expect(root.querySelector('[data-slot="09:00"]')?.textContent).toContain('1 left');
  });

  it('shows the current time of a booking as taken so it cannot be chosen again', async () => {
    const fixture = create({ date: '2026-11-03', currentStartsAtUtc: '2026-11-03T08:00:00+02:00' });
    http.expectOne((r) => r.url === url).flush(anAvailability);
    const root = await settle(fixture);

    const current = root.querySelector<HTMLButtonElement>('[data-slot="08:00"]');
    expect(current?.disabled).toBe(true);
    expect(current?.textContent).toContain('Current time');
  });

  it('emits the slot that was clicked and marks the selected one', async () => {
    const fixture = create({ date: '2026-11-03', selectedStartsAtUtc: '2026-11-03T07:00:00+00:00' });
    let chosen: Slot | undefined;
    fixture.componentInstance.chosen.subscribe((slot) => (chosen = slot));
    http.expectOne((r) => r.url === url).flush(anAvailability);
    const root = await settle(fixture);

    click(root, '[data-slot="08:00"]');

    expect(chosen?.localStart).toBe('08:00');
    expect(root.querySelector('[data-slot="09:00"]')?.getAttribute('aria-pressed')).toBe('true');
    expect(root.querySelector('[data-slot="08:00"]')?.getAttribute('aria-pressed')).toBe('false');
  });

  it('requests the new date when a day is chosen', async () => {
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;
    const tomorrow = addDays(todayIn(zone), 1);

    click(root, `[data-day="${tomorrow}"]`);
    fixture.detectChanges();

    const request = http.expectOne((r) => r.url === url);
    expect(request.request.params.get('date')).toBe(tomorrow);
    request.flush({ ...anAvailability, date: tomorrow, slots: [] });
    await settle(fixture);
    expect(fixture.componentInstance.date()).toBe(tomorrow);
  });

  it('does not allow days before today, or going back before this week', () => {
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;

    const yesterday = addDays(todayIn(zone), -1);
    expect(root.querySelector(`[data-day="${yesterday}"]`)).toBeNull();
    expect(root.querySelector<HTMLButtonElement>('[aria-label="Previous week"]')?.disabled).toBe(true);
  });

  it('moves the strip a week at a time', () => {
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;
    const today = todayIn(zone);

    click(root, '[aria-label="Next week"]');
    fixture.detectChanges();

    expect(root.querySelector(`[data-day="${today}"]`)).toBeNull();
    expect(root.querySelector(`[data-day="${addDays(today, 7)}"]`)).toBeTruthy();
    expect(root.querySelector<HTMLButtonElement>('[aria-label="Previous week"]')?.disabled).toBe(false);
  });

  it('says so when a day has no appointments', async () => {
    const fixture = create({ date: '2026-11-08' });
    http.expectOne((r) => r.url === url).flush({ ...anAvailability, date: '2026-11-08', slots: [] });
    const root = await settle(fixture);

    expect(textOf(root)).toContain('There are no appointments on this day');
  });

  it('shows friendly copy, not the server wording, when the date is outside the booking window', async () => {
    const fixture = create({ date: '2027-06-01' });
    http
      .expectOne((r) => r.url === url)
      .flush({ code: 'date_outside_booking_window', detail: 'internal wording' }, { status: 400, statusText: 'Bad Request' });
    const root = await settle(fixture);

    expect(textOf(root)).toContain('We are not taking bookings for that date');
    expect(textOf(root)).not.toContain('internal wording');
  });

  it('offers a retry after a network failure and reloads', async () => {
    const fixture = create({ date: '2026-11-03' });
    http.expectOne((r) => r.url === url).error(new ProgressEvent('error'));
    const root = await settle(fixture);
    expect(textOf(root)).toContain('No connection');

    click(root, 'button.btn-secondary:not([aria-label])');
    fixture.detectChanges();

    http.expectOne((r) => r.url === url).flush(anAvailability);
  });

  it('has no accessibility violations', async () => {
    const fixture = create({ date: '2026-11-03' });
    http.expectOne((r) => r.url === url).flush(anAvailability);
    const root = await settle(fixture);

    await expectNoA11yViolations(root);
  });
});
