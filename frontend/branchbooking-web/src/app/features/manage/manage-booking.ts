import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { BookingSummary } from '../../shared/booking/booking-summary';
import { Alert } from '../../shared/ui/alert';
import { BookingMessages } from './booking-messages';
import { ReferenceLookup } from './reference-lookup';

@Component({
  selector: 'bb-manage-booking',
  imports: [RouterLink, Alert, BookingSummary, BookingMessages, ReferenceLookup],
  templateUrl: './manage-booking.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageBooking {
  private readonly injector = inject(Injector);
  private readonly session = inject(BookingSessionService);

  protected readonly booking = this.session.booking;
  /** Bumped when the booking changes so the messages panel loads again (WI-505). */
  protected readonly refreshKey = signal(0);

  private readonly detailsHeading = viewChild<ElementRef<HTMLElement>>('detailsHeading');
  private readonly lookupHeading = viewChild<ElementRef<HTMLElement>>('lookupHeading');

  protected focusDetails(): void {
    afterNextRender(() => this.detailsHeading()?.nativeElement.focus(), { injector: this.injector });
  }

  /** Clears the booking from memory, for a shared computer or to look up another one. */
  protected forget(): void {
    this.session.clear();
    afterNextRender(() => this.lookupHeading()?.nativeElement.focus(), { injector: this.injector });
  }
}
