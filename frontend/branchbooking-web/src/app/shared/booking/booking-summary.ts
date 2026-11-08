import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Booking } from '../../core/api/api.models';
import { formatLongDate, formatSlotRange, formatWhen } from '../../core/time/branch-time';
import { StatusChip } from '../ui/status-chip';

/**
 * What a booking is, in words: status, appointment, branch, when (on the branch's clock), who, and what to bring.
 * Used on the confirmation page, the manage page and the cancel and reschedule screens. It never shows the
 * reference; pages that need it show it themselves. Contact details are the API's masked values and are optional.
 */
@Component({
  selector: 'bb-booking-summary',
  imports: [StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dl class="space-y-3">
      <div class="grid gap-1 sm:grid-cols-3">
        <dt class="font-semibold text-brand-900">Status</dt>
        <dd class="sm:col-span-2">
          <bb-status-chip [tone]="booking().status === 'Confirmed' ? 'success' : 'danger'">{{ booking().status }}</bb-status-chip>
          @if (booking().cancelledAtUtc; as cancelledAt) {
            <span class="ml-2 text-sm text-muted">on {{ whenText(cancelledAt) }}</span>
          }
        </dd>
      </div>
      <div class="grid gap-1 sm:grid-cols-3">
        <dt class="font-semibold text-brand-900">Appointment</dt>
        <dd class="sm:col-span-2">{{ booking().appointmentType.name }}</dd>
      </div>
      <div class="grid gap-1 sm:grid-cols-3">
        <dt class="font-semibold text-brand-900">Branch</dt>
        <dd class="sm:col-span-2">
          {{ booking().branch.name }}<br />
          {{ booking().branch.addressLine }}, {{ booking().branch.city }}, {{ booking().branch.province }}
        </dd>
      </div>
      <div class="grid gap-1 sm:grid-cols-3">
        <dt class="font-semibold text-brand-900">When</dt>
        <dd class="sm:col-span-2">
          {{ date() }}<br />
          {{ range() }}
          <span class="block text-sm text-muted">Time at the branch</span>
        </dd>
      </div>
      <div class="grid gap-1 sm:grid-cols-3">
        <dt class="font-semibold text-brand-900">Name</dt>
        <dd class="sm:col-span-2">{{ booking().customerFullName }}</dd>
      </div>
      @if (showContact()) {
        <div class="grid gap-1 sm:grid-cols-3">
          <dt class="font-semibold text-brand-900">Email</dt>
          <dd class="sm:col-span-2">{{ booking().customerEmail }}</dd>
        </div>
        <div class="grid gap-1 sm:grid-cols-3">
          <dt class="font-semibold text-brand-900">Phone</dt>
          <dd class="sm:col-span-2">{{ booking().customerPhone }}</dd>
        </div>
      }
      @if (booking().appointmentType.whatToBring; as bring) {
        <div class="grid gap-1 sm:grid-cols-3">
          <dt class="font-semibold text-brand-900">What to bring</dt>
          <dd class="sm:col-span-2">{{ bring }}</dd>
        </div>
      }
    </dl>
  `,
})
export class BookingSummary {
  readonly booking = input.required<Booking>();
  readonly showContact = input(false);

  protected readonly date = computed(() => formatLongDate(this.booking().startsAtUtc, this.booking().branch.timeZoneId));
  protected readonly range = computed(() =>
    formatSlotRange(this.booking().startsAtUtc, this.booking().endsAtUtc, this.booking().branch.timeZoneId),
  );

  protected whenText(isoInstant: string): string {
    return formatWhen(isoInstant, this.booking().branch.timeZoneId);
  }
}
