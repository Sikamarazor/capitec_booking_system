import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../../core/api/api-error.interceptor';
import { aBranch, anAvailability } from '../../../testing/api-fixtures';
import { buttonWithText, click, textOf } from '../../../testing/dom';
import { BookingDraftService } from '../booking-draft.service';
import { TimeStep } from './time-step';

describe('TimeStep', () => {
  let http: HttpTestingController;
  let draft: BookingDraftService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    draft = TestBed.inject(BookingDraftService);
  });

  afterEach(() => http.verify());

  it('asks the customer to go back when no branch was chosen', () => {
    const fixture = TestBed.createComponent(TimeStep);
    fixture.detectChanges();

    expect(textOf(fixture.nativeElement as HTMLElement)).toContain('Choose a branch first');
  });

  it('shows the branch time note, and the chosen time once a slot is picked', async () => {
    draft.chooseBranch(aBranch);
    draft.date.set('2026-11-03');
    const fixture = TestBed.createComponent(TimeStep);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/v1/branches/branch-1/availability').flush(anAvailability);
    fixture.detectChanges();
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(textOf(root)).toContain('Times are on the clock at Test Branch');

    click(root, '[data-slot="08:00"]');
    fixture.detectChanges();

    expect(draft.slot()?.localStart).toBe('08:00');
    expect(textOf(root)).toContain('You chose');
    expect(textOf(root)).toContain('at 08:00');
  });

  it('does not continue without a time', () => {
    draft.chooseBranch(aBranch);
    const fixture = TestBed.createComponent(TimeStep);
    fixture.detectChanges();
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    click(fixture.nativeElement as HTMLElement, 'button[type="submit"]');
    fixture.detectChanges();

    expect(done).toBe(0);
    expect(textOf(fixture.nativeElement as HTMLElement)).toContain('Choose a time to continue');
  });

  it('goes back', () => {
    draft.chooseBranch(aBranch);
    const fixture = TestBed.createComponent(TimeStep);
    fixture.detectChanges();
    let back = 0;
    fixture.componentInstance.back.subscribe(() => back++);

    buttonWithText(fixture.nativeElement as HTMLElement, 'Back')?.click();

    expect(back).toBe(1);
  });
});
