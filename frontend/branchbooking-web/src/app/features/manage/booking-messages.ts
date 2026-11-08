import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { BookingMessage, MessageKind, MessageStatus } from '../../core/api/api.models';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../core/api/error-messages';
import { formatWhen } from '../../core/time/branch-time';
import { Alert } from '../../shared/ui/alert';
import { Loading } from '../../shared/ui/loading';
import { ChipTone, StatusChip } from '../../shared/ui/status-chip';

type LoadState = 'idle' | 'loading' | 'loaded' | 'failed';

const KIND_LABELS: Record<MessageKind, string> = {
  Confirmation: 'Booking confirmation',
  Cancellation: 'Cancellation',
  Reschedule: 'Appointment moved',
  Reminder: 'Reminder',
};

const STATUS: Record<MessageStatus, { label: string; tone: ChipTone }> = {
  Pending: { label: 'Waiting to send', tone: 'neutral' },
  Sent: { label: 'Sent', tone: 'success' },
  Failed: { label: 'Could not be sent', tone: 'danger' },
  Skipped: { label: 'Not sent', tone: 'warning' },
};

/** The messages the system produced for a booking, oldest first, with the simulated channel labelled as a demo. */
@Component({
  selector: 'bb-booking-messages',
  imports: [Alert, Loading, StatusChip],
  templateUrl: './booking-messages.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingMessages {
  private readonly api = inject(BranchBookingApiService);

  readonly reference = input.required<string>();
  readonly timeZoneId = input.required<string>();
  /** Change this value to make the panel load again (the manage page does after a cancellation or a move). */
  readonly refresh = input(0);

  protected readonly state = signal<LoadState>('idle');
  protected readonly messages = signal<BookingMessage[]>([]);
  protected readonly error = signal<ErrorDescription | null>(null);
  private request?: Subscription;

  constructor() {
    effect(() => {
      const reference = this.reference();
      this.refresh();
      untracked(() => this.load(reference));
    });
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  protected reload(): void {
    this.load(this.reference());
  }

  protected kindLabel(message: BookingMessage): string {
    return KIND_LABELS[message.kind] ?? message.kind;
  }

  protected status(message: BookingMessage): { label: string; tone: ChipTone } {
    return STATUS[message.status] ?? { label: message.status, tone: 'neutral' };
  }

  protected whenText(message: BookingMessage): string {
    return formatWhen(message.sentAtUtc ?? message.createdAtUtc, this.timeZoneId());
  }

  protected isDemo(message: BookingMessage): boolean {
    return message.channel === 'Simulated';
  }

  private load(reference: string): void {
    this.request?.unsubscribe();
    this.state.set('loading');
    this.error.set(null);

    this.request = this.api.getMessages(reference).subscribe({
      next: (messages) => {
        this.messages.set(messages);
        this.state.set('loaded');
      },
      error: (error: unknown) => {
        this.error.set(describeError(error));
        this.state.set('failed');
      },
    });
  }
}
