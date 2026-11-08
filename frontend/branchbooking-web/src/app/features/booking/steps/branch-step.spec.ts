import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../../../core/api/api-error.interceptor';
import { expectNoA11yViolations } from '../../../testing/a11y';
import { aBranch, anAvailability } from '../../../testing/api-fixtures';
import { buttonWithText, click, setValue, textOf } from '../../../testing/dom';
import { BookingDraftService } from '../booking-draft.service';
import { BranchStep } from './branch-step';

describe('BranchStep', () => {
  const sandton = { ...aBranch, id: 'branch-2', name: 'Sandton Test Branch', city: 'Sandton' };
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function loaded() {
    const fixture = TestBed.createComponent(BranchStep);
    fixture.detectChanges();
    http.expectOne('/api/v1/branches').flush([aBranch, sandton]);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  it('lists the branches with their addresses and a count', async () => {
    const root = (await loaded()).nativeElement as HTMLElement;

    expect(root.querySelectorAll('input[type="radio"]').length).toBe(2);
    expect(textOf(root)).toContain('1 Test Street, Johannesburg');
    expect(textOf(root)).toContain('2 branches shown');
  });

  it('filters by name or town and says when nothing matches', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;

    setValue(root, '#branch-search', 'sandton');
    fixture.detectChanges();
    expect(root.querySelectorAll('input[type="radio"]').length).toBe(1);
    expect(textOf(root)).toContain('1 branch shown');

    setValue(root, '#branch-search', 'zzz');
    fixture.detectChanges();
    expect(textOf(root)).toContain('No branch matches that search');
  });

  it('records the choice and continues', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    click(root, 'input[type="radio"]');
    fixture.detectChanges();
    click(root, 'button[type="submit"]');

    expect(TestBed.inject(BookingDraftService).branch()?.id).toBe('branch-1');
    expect(done).toBe(1);
  });

  it('does not continue without a choice', async () => {
    const fixture = await loaded();
    const root = fixture.nativeElement as HTMLElement;
    let done = 0;
    fixture.componentInstance.done.subscribe(() => done++);

    click(root, 'button[type="submit"]');
    fixture.detectChanges();

    expect(done).toBe(0);
    expect(textOf(root)).toContain('Choose a branch to continue');
  });

  it('forgets the chosen time when a different branch is picked', async () => {
    const fixture = await loaded();
    const draft = TestBed.inject(BookingDraftService);
    draft.chooseBranch(aBranch);
    draft.slot.set(anAvailability.slots[0]);
    fixture.detectChanges();

    const radios = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="radio"]');
    radios[1].click();

    expect(draft.branch()?.id).toBe('branch-2');
    expect(draft.slot()).toBeNull();
  });

  it('goes back', async () => {
    const fixture = await loaded();
    let back = 0;
    fixture.componentInstance.back.subscribe(() => back++);

    buttonWithText(fixture.nativeElement as HTMLElement, 'Back')?.click();

    expect(back).toBe(1);
  });

  it('has no accessibility violations', async () => {
    const fixture = await loaded();

    await expectNoA11yViolations(fixture.nativeElement as HTMLElement);
  });
});
