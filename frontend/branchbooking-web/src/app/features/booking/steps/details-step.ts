import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiError } from '../../../core/api/api-error';
import { BranchBookingApiService } from '../../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../../core/api/error-messages';
import { BookingSessionService } from '../../../core/session/booking-session.service';
import { formatWhen } from '../../../core/time/branch-time';
import {
  CustomerField,
  customerFieldMessage,
  emailValidator,
  fullNameValidator,
  phoneValidator,
} from '../../../core/validation/customer-validation';
import { Alert } from '../../../shared/ui/alert';
import { describedBy } from '../../../shared/ui/described-by';
import { ErrorSummary, ErrorSummaryItem } from '../../../shared/ui/error-summary';
import { FormField } from '../../../shared/ui/form-field';
import { BookingDraftService } from '../booking-draft.service';

/** Sent up to the wizard when the customer must go back to an earlier step. */
export interface StepNotice {
  step: number;
  error: ErrorDescription;
}

const CUSTOMER_FIELDS: CustomerField[] = ['fullName', 'email', 'phone'];

@Component({
  selector: 'bb-details-step',
  imports: [ReactiveFormsModule, Alert, FormField, ErrorSummary],
  templateUrl: './details-step.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DetailsStep {
  private readonly api = inject(BranchBookingApiService);
  private readonly router = inject(Router);
  private readonly session = inject(BookingSessionService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly draft = inject(BookingDraftService);

  readonly back = output<void>();
  readonly lost = output<StepNotice>();

  private readonly summary = viewChild(ErrorSummary);

  protected readonly form = new FormGroup({
    fullName: new FormControl(this.draft.details().fullName, { nonNullable: true, validators: [fullNameValidator] }),
    email: new FormControl(this.draft.details().email, { nonNullable: true, validators: [emailValidator] }),
    phone: new FormControl(this.draft.details().phone, { nonNullable: true, validators: [phoneValidator] }),
  });

  protected readonly attempted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly summaryItems = signal<ErrorSummaryItem[]>([]);
  protected readonly describedBy = describedBy;

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) =>
      this.draft.details.set({
        fullName: value.fullName ?? '',
        email: value.email ?? '',
        phone: value.phone ?? '',
      }),
    );
  }

  protected whenText(): string | null {
    const slot = this.draft.slot();
    const branch = this.draft.branch();
    return slot && branch ? formatWhen(slot.startsAtUtc, branch.timeZoneId) : null;
  }

  /** The message under a field, shown once the field was touched or a submit was attempted. */
  protected message(field: CustomerField): string | null {
    const control = this.form.controls[field];
    return this.attempted() || control.touched ? customerFieldMessage(field, control.errors) : null;
  }

  protected describe(field: CustomerField): string | null {
    return describedBy(field, false, this.message(field) !== null);
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    this.attempted.set(true);
    this.error.set(null);

    const summary = this.collectProblems();
    this.summaryItems.set(summary);
    if (summary.length > 0) {
      queueMicrotask(() => this.summary()?.focus());
      return;
    }

    const type = this.draft.appointmentType();
    const branch = this.draft.branch();
    const slot = this.draft.slot();
    if (!type || !branch || !slot) {
      this.lost.emit({
        step: !type ? 0 : !branch ? 1 : 2,
        error: describeError(new ApiError(400, 'validation_failed')),
      });
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.api
      .createBooking({
        branchId: branch.id,
        appointmentTypeId: type.id,
        startsAtUtc: slot.startsAtUtc,
        fullName: value.fullName.trim(),
        email: value.email.trim(),
        phone: value.phone.trim(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (booking) => {
          this.session.set(booking);
          this.draft.reset();
          void this.router.navigateByUrl('/confirmation');
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          this.handleFailure(error);
        },
      });
  }

  private collectProblems(): ErrorSummaryItem[] {
    return CUSTOMER_FIELDS.flatMap((field) => {
      const message = customerFieldMessage(field, this.form.controls[field].errors);
      return message ? [{ id: field, message }] : [];
    });
  }

  private handleFailure(error: unknown): void {
    const description = describeError(error);
    const code = error instanceof ApiError ? error.code : '';

    switch (code) {
      case 'slot_not_available':
      case 'slot_too_soon':
      case 'slot_not_offered':
      case 'date_outside_booking_window':
        this.draft.slot.set(null);
        this.lost.emit({ step: 2, error: description });
        return;
      case 'branch_not_found':
        this.draft.branch.set(null);
        this.draft.slot.set(null);
        this.lost.emit({ step: 1, error: description });
        return;
      case 'appointment_type_not_found':
        this.draft.appointmentType.set(null);
        this.lost.emit({ step: 0, error: description });
        return;
      case 'validation_failed':
      case 'invalid_customer_details':
        this.markServerRejections(error as ApiError, description);
        return;
      default:
        this.error.set(description);
    }
  }

  /** The server refused some details. Show our own copy against the fields it named, or a general message. */
  private markServerRejections(error: ApiError, description: ErrorDescription): void {
    const named = CUSTOMER_FIELDS.filter((field) => field in error.fieldErrors);
    if (named.length === 0) {
      this.error.set(description);
      return;
    }

    for (const field of named) {
      this.form.controls[field].setErrors({ server: true });
      this.form.controls[field].markAsTouched();
    }

    this.summaryItems.set(this.collectProblems());
    queueMicrotask(() => this.summary()?.focus());
  }
}
