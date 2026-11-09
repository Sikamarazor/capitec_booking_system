export interface AppointmentType {
  id: string;
  code: string;
  name: string;
  description: string;
  whatToBring: string;
}

export interface OpeningHours {
  day: string;
  opensAt: string;
  closesAt: string;
}

export interface Branch {
  id: string;
  code: string;
  name: string;
  addressLine: string;
  city: string;
  province: string;
  timeZoneId: string;
  slotLengthMinutes: number;
  openingHours: OpeningHours[];
}

export interface Slot {
  startsAtUtc: string;
  endsAtUtc: string;
  /** Branch wall-clock start, `HH:mm`. */
  localStart: string;
  remaining: number;
  isAvailable: boolean;
}

export interface Availability {
  branchId: string;
  /** Branch-local calendar date, `YYYY-MM-DD`. */
  date: string;
  timeZoneId: string;
  slots: Slot[];
}

export interface BookingAppointmentType {
  id: string;
  code: string;
  name: string;
  whatToBring: string;
}

export interface BookingBranch {
  id: string;
  name: string;
  addressLine: string;
  city: string;
  province: string;
  timeZoneId: string;
}

export type BookingStatus = 'Confirmed' | 'Cancelled';

export interface Booking {
  reference: string;
  status: BookingStatus;
  appointmentType: BookingAppointmentType;
  branch: BookingBranch;
  startsAtUtc: string;
  endsAtUtc: string;
  startsAtLocal: string;
  customerFullName: string;
  /** Masked by the API, for example `t*****@example.com`. */
  customerEmail: string;
  /** Masked by the API, last three digits only. */
  customerPhone: string;
  rescheduleCount: number;
  canCancel: boolean;
  canReschedule: boolean;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
}

export interface CreateBookingRequest {
  branchId: string;
  appointmentTypeId: string;
  startsAtUtc: string;
  fullName: string;
  email: string;
  phone: string;
}

export type MessageKind = 'Confirmation' | 'Cancellation' | 'Reschedule' | 'Reminder';
export type MessageStatus = 'Pending' | 'Sent' | 'Failed' | 'Skipped';

export interface BookingMessage {
  kind: MessageKind;
  status: MessageStatus;
  channel: string | null;
  createdAtUtc: string;
  sentAtUtc: string | null;
  /** Only present for the simulated channel. */
  subject: string | null;
  /** Only present for the simulated channel. */
  body: string | null;
}
