import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AppointmentType } from '../../../core/api/api.models';
import { BranchBookingApiService } from '../../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../../core/api/error-messages';
import { Alert } from '../../../shared/ui/alert';
import { Loading } from '../../../shared/ui/loading';
import { BookingDraftService } from '../booking-draft.service';

@Component({
  selector: 'bb-type-step',
  imports: [Alert, Loading],
  templateUrl: './type-step.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TypeStep {
  private readonly api = inject(BranchBookingApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly draft = inject(BookingDraftService);

  readonly done = output<void>();

  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly types = signal<AppointmentType[]>([]);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly missing = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.state.set('loading');
    this.api
      .getAppointmentTypes()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (types) => {
          this.types.set(types);
          this.state.set('ready');
        },
        error: (error: unknown) => {
          this.error.set(describeError(error));
          this.state.set('error');
        },
      });
  }

  protected isSelected(type: AppointmentType): boolean {
    return this.draft.appointmentType()?.id === type.id;
  }

  protected select(type: AppointmentType): void {
    this.draft.appointmentType.set(type);
    this.missing.set(false);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (!this.draft.appointmentType()) {
      this.missing.set(true);
      return;
    }
    this.done.emit();
  }
}
