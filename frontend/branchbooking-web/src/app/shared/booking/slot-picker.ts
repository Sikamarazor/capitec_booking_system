import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  untracked,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { Availability, Slot } from '../../core/api/api.models';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../core/api/error-messages';
import { addDays, compareDates, dayParts, formatLongDate, sameInstant, todayIn } from '../../core/time/branch-time';
import { Alert } from '../ui/alert';
import { Loading } from '../ui/loading';

type LoadState = 'idle' | 'loading' | 'ready' | 'error';

/**
 * Picks a day from a one-week strip and a time from that day's slots at one branch. It shows what the API says
 * is offered and decides nothing itself. Reused by the booking wizard and by rescheduling.
 */
@Component({
  selector: 'bb-slot-picker',
  imports: [Alert, Loading],
  templateUrl: './slot-picker.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlotPicker {
  private readonly api = inject(BranchBookingApiService);

  readonly branchId = input.required<string>();
  readonly timeZoneId = input.required<string>();
  /** The chosen branch-local date, `YYYY-MM-DD`. Two-way: `[(date)]`. */
  readonly date = model<string | null>(null);
  readonly selectedStartsAtUtc = input<string | null>(null);
  /** An existing time that is shown but cannot be chosen (rescheduling). */
  readonly currentStartsAtUtc = input<string | null>(null);
  readonly chosen = output<Slot>();

  protected readonly today = computed(() => todayIn(this.timeZoneId()));
  private readonly weekStart = signal<string | null>(null);
  protected readonly visibleStart = computed(() => this.weekStart() ?? this.date() ?? this.today());
  protected readonly canGoBack = computed(() => compareDates(this.visibleStart(), this.today()) > 0);

  protected readonly days = computed(() =>
    Array.from({ length: 7 }, (_, index) => {
      const value = addDays(this.visibleStart(), index);
      return {
        value,
        parts: dayParts(value),
        label: formatLongDate(`${value}T12:00:00Z`, 'UTC'),
        past: compareDates(value, this.today()) < 0,
      };
    }),
  );

  protected readonly state = signal<LoadState>('idle');
  protected readonly availability = signal<Availability | null>(null);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly anyAvailable = computed(() => this.availability()?.slots.some((s) => s.isAvailable) ?? false);
  private request?: Subscription;

  constructor() {
    effect(() => {
      const branchId = this.branchId();
      const date = this.date();
      untracked(() => this.load(branchId, date));
    });
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  protected pick(day: { value: string; past: boolean }): void {
    if (!day.past) {
      this.date.set(day.value);
    }
  }

  protected previousWeek(): void {
    const earlier = addDays(this.visibleStart(), -7);
    this.weekStart.set(compareDates(earlier, this.today()) < 0 ? this.today() : earlier);
  }

  protected nextWeek(): void {
    this.weekStart.set(addDays(this.visibleStart(), 7));
  }

  protected reload(): void {
    this.load(this.branchId(), this.date());
  }

  protected isCurrent(slot: Slot): boolean {
    return sameInstant(slot.startsAtUtc, this.currentStartsAtUtc());
  }

  protected isSelected(slot: Slot): boolean {
    return sameInstant(slot.startsAtUtc, this.selectedStartsAtUtc());
  }

  private load(branchId: string, date: string | null): void {
    this.request?.unsubscribe();
    this.error.set(null);

    if (!date) {
      this.state.set('idle');
      this.availability.set(null);
      return;
    }

    this.state.set('loading');
    this.request = this.api.getAvailability(branchId, date).subscribe({
      next: (availability) => {
        this.availability.set(availability);
        this.state.set('ready');
      },
      error: (error: unknown) => {
        this.availability.set(null);
        this.error.set(describeError(error));
        this.state.set('error');
      },
    });
  }
}
