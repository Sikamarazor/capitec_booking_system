import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Booking } from '../../core/api/api.models';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../core/api/error-messages';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { formatWhen } from '../../core/time/branch-time';
import { Alert } from '../../shared/ui/alert';
import { FormField } from '../../shared/ui/form-field';
import { isStaleBookingError } from './stale-booking-error';

export const CANCEL_REASON_MAX_LENGTH = 500;

@Component({
  selector: 'bb-cancel-booking',
  imports: [ReactiveFormsModule, Alert, FormField],
  templateUrl: './cancel-booking.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CancelBooking {
  private readonly api = inject(BranchBookingApiService);
  private readonly session = inject(BookingSessionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly booking = input.required<Booking>();
  /** The booking was cancelled; the session already holds the updated booking. */
  readonly done = output<void>();
  /** The customer chose to keep the appointment. */
  readonly dismissed = output<void>();
  /** The server says the booking is no longer as shown; the page should load it again. */
  readonly stale = output<ErrorDescription>();

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly maxLength = CANCEL_REASON_MAX_LENGTH;
  protected readonly reason = new FormControl('', { nonNullable: true });
  protected readonly submitting = signal(false);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly when = computed(() => formatWhen(this.booking().startsAtUtc, this.booking().branch.timeZoneId));

  constructor() {
    afterNextRender(() => this.heading()?.nativeElement.focus());
  }

  protected confirm(): void {
    if (this.submitting()) {
      return;
    }

    this.error.set(null);
    this.submitting.set(true);
    this.api
      .cancelBooking(this.booking().reference, this.reason.value)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.session.set(updated);
          this.submitting.set(false);
          this.done.emit();
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const description = describeError(error);
          if (isStaleBookingError(error)) {
            this.stale.emit(description);
          } else {
            this.error.set(description);
          }
        },
      });
  }
}
