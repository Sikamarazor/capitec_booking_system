import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../core/api/error-messages';
import { normaliseReference } from '../../core/reference/booking-reference';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { Alert } from '../../shared/ui/alert';
import { describedBy } from '../../shared/ui/described-by';
import { ErrorSummary, ErrorSummaryItem } from '../../shared/ui/error-summary';
import { FormField } from '../../shared/ui/form-field';

/** Asks for a booking reference and, if the API knows it, puts the booking in the session. */
@Component({
  selector: 'bb-reference-lookup',
  imports: [ReactiveFormsModule, Alert, FormField, ErrorSummary],
  templateUrl: './reference-lookup.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReferenceLookup {
  private readonly api = inject(BranchBookingApiService);
  private readonly session = inject(BookingSessionService);
  private readonly destroyRef = inject(DestroyRef);

  /** Emitted after the booking has been placed in the session. */
  readonly found = output<void>();

  private readonly summary = viewChild(ErrorSummary);

  protected readonly reference = new FormControl('', { nonNullable: true });
  protected readonly fieldMessage = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly summaryItems = computed<ErrorSummaryItem[]>(() => {
    const message = this.fieldMessage();
    return message ? [{ id: 'reference', message }] : [];
  });
  protected readonly describedBy = describedBy;

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    this.error.set(null);
    const raw = this.reference.value;
    const normalised = normaliseReference(raw);

    if (!normalised) {
      this.fieldMessage.set(
        raw.trim().length === 0 ? 'Enter your booking reference.' : 'Enter your reference like BK-7H2QX-M9RTD.',
      );
      queueMicrotask(() => this.summary()?.focus());
      return;
    }

    this.fieldMessage.set(null);
    this.submitting.set(true);
    this.api
      .getBooking(normalised)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (booking) => {
          this.session.set(booking);
          this.reference.reset('');
          this.submitting.set(false);
          this.found.emit();
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          this.error.set(describeError(error));
        },
      });
  }
}
