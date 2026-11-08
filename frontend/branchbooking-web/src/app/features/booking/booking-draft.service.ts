import { Injectable, signal } from '@angular/core';
import { AppointmentType, Branch, Slot } from '../../core/api/api.models';

export const STEP_LABELS = ['Appointment', 'Branch', 'Time', 'Your details'];
export const STEP_TITLES = ['What do you need?', 'Choose a branch', 'Choose a date and time', 'Your details'];

export interface CustomerDraft {
  fullName: string;
  email: string;
  phone: string;
}

const EMPTY_DETAILS: CustomerDraft = { fullName: '', email: '', phone: '' };

/** What the customer has chosen so far. Cleared when the wizard is left and after a successful booking. */
@Injectable({ providedIn: 'root' })
export class BookingDraftService {
  readonly step = signal(0);
  readonly appointmentType = signal<AppointmentType | null>(null);
  readonly branch = signal<Branch | null>(null);
  /** Branch-local calendar date, `YYYY-MM-DD`. */
  readonly date = signal<string | null>(null);
  readonly slot = signal<Slot | null>(null);
  readonly details = signal<CustomerDraft>({ ...EMPTY_DETAILS });

  /** Choosing a different branch invalidates the time chosen at the old one. */
  chooseBranch(branch: Branch): void {
    if (this.branch()?.id !== branch.id) {
      this.slot.set(null);
      this.date.set(null);
    }
    this.branch.set(branch);
  }

  reset(): void {
    this.step.set(0);
    this.appointmentType.set(null);
    this.branch.set(null);
    this.date.set(null);
    this.slot.set(null);
    this.details.set({ ...EMPTY_DETAILS });
  }
}
