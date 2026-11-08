import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ErrorDescription } from '../../core/api/error-messages';
import { Alert } from '../../shared/ui/alert';
import { Stepper } from '../../shared/ui/stepper';
import { BookingDraftService, STEP_LABELS, STEP_TITLES } from './booking-draft.service';
import { BranchStep } from './steps/branch-step';
import { DetailsStep, StepNotice } from './steps/details-step';
import { TimeStep } from './steps/time-step';
import { TypeStep } from './steps/type-step';

@Component({
  selector: 'bb-booking-wizard',
  imports: [Alert, Stepper, TypeStep, BranchStep, TimeStep, DetailsStep],
  templateUrl: './booking-wizard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingWizard {
  private readonly injector = inject(Injector);
  protected readonly draft = inject(BookingDraftService);
  protected readonly labels = STEP_LABELS;
  protected readonly titles = STEP_TITLES;

  /** Why the customer was sent back to an earlier step (for example, the time was just taken). */
  protected readonly notice = signal<ErrorDescription | null>(null);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('stepHeading');

  constructor() {
    // Do not keep a customer's half-typed details in memory after they leave.
    inject(DestroyRef).onDestroy(() => this.draft.reset());
  }

  protected next(): void {
    this.notice.set(null);
    this.goTo(Math.min(this.draft.step() + 1, this.labels.length - 1));
  }

  protected back(): void {
    this.notice.set(null);
    this.goTo(Math.max(this.draft.step() - 1, 0));
  }

  protected sentBack(notice: StepNotice): void {
    this.notice.set(notice.error);
    this.goTo(notice.step);
  }

  private goTo(step: number): void {
    this.draft.step.set(step);
    afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
  }
}
