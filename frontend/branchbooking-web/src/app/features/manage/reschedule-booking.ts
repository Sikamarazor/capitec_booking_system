import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiError } from '../../core/api/api-error';
import { Booking, Slot } from '../../core/api/api.models';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../core/api/error-messages';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { formatWhen } from '../../core/time/branch-time';
import { SlotPicker } from '../../shared/booking/slot-picker';
import { Alert } from '../../shared/ui/alert';
import { isStaleBookingError } from './stale-booking-error';

/** Refusals that mean the time the customer picked cannot be had. They stay on this screen and choose again. */
const SLOT_LOST_CODES: readonly string[] = [
  'slot_not_available',
  'slot_too_soon',
  'slot_not_offered',
  'date_outside_booking_window',
  'same_slot',
];

@Component({
  selector: 'bb-reschedule-booking',
  imports: [Alert, SlotPicker],
  templateUrl: './reschedule-booking.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RescheduleBooking {
  private readonly api = inject(BranchBookingApiService);
  private readonly session = inject(BookingSessionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly booking = input.required<Booking>();
  /** The booking was moved; the session already holds the updated booking. */
  readonly done = output<void>();
  /** The customer chose to keep the current time. */
  readonly dismissed = output<void>();
  /** The server says the booking is no longer as shown; the page should load it again. */
  readonly stale = output<ErrorDescription>();

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  /** Starts on the day of the current appointment (the branch-local date is the first ten characters). */
  protected readonly date = linkedSignal<string | null>(() => this.booking().startsAtLocal.slice(0, 10));
  protected readonly slot = signal<Slot | null>(null);
  protected readonly pickerRefresh = signal(0);
  protected readonly submitting = signal(false);
  protected readonly error = signal<ErrorDescription | null>(null);

  protected readonly currentText = computed(() => formatWhen(this.booking().startsAtUtc, this.booking().branch.timeZoneId));
  protected readonly newText = computed(() => {
    const slot = this.slot();
    return slot ? formatWhen(slot.startsAtUtc, this.booking().branch.timeZoneId) : null;
  });

  constructor() {
    afterNextRender(() => this.heading()?.nativeElement.focus());
  }

  protected choose(slot: Slot): void {
    this.error.set(null);
    this.slot.set(slot);
  }

  protected confirm(): void {
    const slot = this.slot();
    if (!slot || this.submitting()) {
      return;
    }

    this.error.set(null);
    this.submitting.set(true);
    this.api
      .rescheduleBooking(this.booking().reference, slot.startsAtUtc)
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
            return;
          }

          if (error instanceof ApiError && SLOT_LOST_CODES.includes(error.code)) {
            this.slot.set(null);
            this.pickerRefresh.update((n) => n + 1);
          }
          this.error.set(description);
        },
      });
  }
}
