import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription } from '../../core/api/error-messages';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { formatWhen } from '../../core/time/branch-time';
import { BookingSummary } from '../../shared/booking/booking-summary';
import { Alert, AlertVariant } from '../../shared/ui/alert';
import { BookingMessages } from './booking-messages';
import { CancelBooking } from './cancel-booking';
import { ReferenceLookup } from './reference-lookup';
import { RescheduleBooking } from './reschedule-booking';

type Mode = 'view' | 'cancel' | 'reschedule';

interface Notice {
  variant: AlertVariant;
  heading: string;
  text: string;
}

@Component({
  selector: 'bb-manage-booking',
  imports: [RouterLink, Alert, BookingSummary, BookingMessages, ReferenceLookup, CancelBooking, RescheduleBooking],
  templateUrl: './manage-booking.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageBooking {
  private readonly injector = inject(Injector);
  private readonly api = inject(BranchBookingApiService);
  private readonly session = inject(BookingSessionService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly booking = this.session.booking;
  protected readonly mode = signal<Mode>('view');
  protected readonly notice = signal<Notice | null>(null);
  /** Bumped when the booking changes so the messages panel loads again. */
  protected readonly refreshKey = signal(0);

  private readonly detailsHeading = viewChild<ElementRef<HTMLElement>>('detailsHeading');
  private readonly lookupHeading = viewChild<ElementRef<HTMLElement>>('lookupHeading');

  protected focusDetails(): void {
    afterNextRender(() => this.detailsHeading()?.nativeElement.focus(), { injector: this.injector });
  }

  protected open(mode: 'cancel' | 'reschedule'): void {
    this.notice.set(null);
    this.mode.set(mode);
  }

  protected dismiss(): void {
    this.mode.set('view');
    this.focusDetails();
  }

  protected finished(kind: 'cancelled' | 'rescheduled'): void {
    const booking = this.booking();
    this.mode.set('view');
    this.refreshKey.update((n) => n + 1);
    this.notice.set(
      kind === 'cancelled'
        ? {
            variant: 'success',
            heading: 'Appointment cancelled',
            text: 'Your appointment has been cancelled and the time released. We have queued a message to confirm it.',
          }
        : {
            variant: 'success',
            heading: 'Appointment moved',
            text: `Your appointment is now ${booking ? formatWhen(booking.startsAtUtc, booking.branch.timeZoneId) : 'at the new time'}. We have queued a message to confirm it.`,
          },
    );
    this.focusDetails();
  }

  /** The server says the booking is not as the screen shows it: say why, and load it again. */
  protected refreshAfter(problem: ErrorDescription): void {
    const booking = this.booking();
    this.mode.set('view');
    this.notice.set({ variant: 'warning', heading: problem.title, text: problem.message });
    this.refreshKey.update((n) => n + 1);
    this.focusDetails();

    if (booking) {
      this.api
        .getBooking(booking.reference)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (fresh) => this.session.set(fresh),
          // The notice already says what happened; looking the booking up again is the customer's fallback.
          error: () => undefined,
        });
    }
  }

  /** Clears the booking from memory, for a shared computer or to look up another one. */
  protected forget(): void {
    this.session.clear();
    this.mode.set('view');
    this.notice.set(null);
    afterNextRender(() => this.lookupHeading()?.nativeElement.focus(), { injector: this.injector });
  }
}
