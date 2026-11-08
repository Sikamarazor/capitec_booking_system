import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../../core/api/api-error.interceptor';
import { expectNoA11yViolations } from '../../../testing/a11y';
import { aType } from '../../../testing/api-fixtures';
import { click, textOf } from '../../../testing/dom';
import { BookingDraftService } from '../booking-draft.service';
import { TypeStep } from './type-step';

describe('TypeStep', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function loaded(types = [aType, { ...aType, id: 'type-2', name: 'Collect a card' }]) {
    const fixture = TestBed.createComponent(TypeStep);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').flush(types);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('lists the appointment types as radio choices', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelectorAll('input[type="radio"]').length).toBe(2);
    expect(textOf(root)).toContain('Collect a card');
  });

  it('records the choice and continues', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    click(root, 'input[type="radio"]');
    fixture.detectChanges();
    click(root, 'button[type="submit"]');

    expect(TestBed.inject(BookingDraftService).appointmentType()?.id).toBe('type-1');
    expect(done).toBe(1);
  });

  it('does not continue without a choice and says what is missing', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    expect(done).toBe(0);
    expect(textOf(root)).toContain('Choose an appointment type to continue');
  });

  it('shows an error with a retry when loading fails', async () => {
    const fixture = TestBed.createComponent(TypeStep);
    fixture.detectChanges();
    http.expectOne('/api/v1/appointment-types').error(new ProgressEvent('error'));
    fixture.detectChanges();
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(textOf(root)).toContain('No connection');

    click(root, 'button.btn-secondary');
    http.expectOne('/api/v1/appointment-types').flush([aType]);
  });

  it('has no accessibility violations', async () => {
    const fixture = await loaded();

    await expectNoA11yViolations(fixture.nativeElement as HTMLElement);
  });
});
