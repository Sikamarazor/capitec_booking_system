import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { Alert } from '../../../shared/ui/alert';
import { SlotPicker } from '../../../shared/booking/slot-picker';
import { formatWhen } from '../../../core/time/branch-time';
import { BookingDraftService } from '../booking-draft.service';

@Component({
  selector: 'bb-time-step',
  imports: [Alert, SlotPicker],
  templateUrl: './time-step.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeStep {
  protected readonly draft = inject(BookingDraftService);

  readonly done = output<void>();
  readonly back = output<void>();

  protected readonly missing = signal(false);

  protected chosenText(): string | null {
    const slot = this.draft.slot();
    const branch = this.draft.branch();
    return slot && branch ? formatWhen(slot.startsAtUtc, branch.timeZoneId) : null;
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (!this.draft.slot()) {
      this.missing.set(true);
      return;
    }
    this.done.emit();
  }
}
