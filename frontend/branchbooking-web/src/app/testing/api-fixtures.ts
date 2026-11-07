import { AppointmentType, Availability, Booking, BookingMessage, Branch } from '../core/api/api.models';

export const aType: AppointmentType = {
  id: 'type-1',
  code: 'OPEN_ACCOUNT',
  name: 'Open an account',
  description: 'Open a new account with a consultant.',
  whatToBring: 'Bring your ID document.',
};

export const aBranch: Branch = {
  id: 'branch-1',
  code: 'TEST-001',
  name: 'Test Branch',
  addressLine: '1 Test Street',
  city: 'Johannesburg',
  province: 'Gauteng',
  timeZoneId: 'Africa/Johannesburg',
  slotLengthMinutes: 30,
  openingHours: [{ day: 'Tuesday', opensAt: '08:00', closesAt: '16:00' }],
};

export const anAvailability: Availability = {
  branchId: 'branch-1',
  date: '2026-11-03',
  timeZoneId: 'Africa/Johannesburg',
  slots: [
    { startsAtUtc: '2026-11-03T06:00:00+00:00', endsAtUtc: '2026-11-03T06:30:00+00:00', localStart: '08:00', remaining: 2, isAvailable: true },
    { startsAtUtc: '2026-11-03T06:30:00+00:00', endsAtUtc: '2026-11-03T07:00:00+00:00', localStart: '08:30', remaining: 0, isAvailable: false },
    { startsAtUtc: '2026-11-03T07:00:00+00:00', endsAtUtc: '2026-11-03T07:30:00+00:00', localStart: '09:00', remaining: 1, isAvailable: true },
  ],
};

export function aBooking(overrides: Partial<Booking> = {}): Booking {
  return {
    reference: 'BK-7H2QX-M9RTD',
    status: 'Confirmed',
    appointmentType: { id: 'type-1', code: 'OPEN_ACCOUNT', name: 'Open an account', whatToBring: 'Bring your ID document.' },
    branch: {
      id: 'branch-1',
      name: 'Test Branch',
      addressLine: '1 Test Street',
      city: 'Johannesburg',
      province: 'Gauteng',
      timeZoneId: 'Africa/Johannesburg',
    },
    startsAtUtc: '2026-11-03T06:00:00+00:00',
    endsAtUtc: '2026-11-03T06:30:00+00:00',
    startsAtLocal: '2026-11-03T08:00:00+02:00',
    customerFullName: 'Thandi Nkosi',
    customerEmail: 't*****@example.com',
    customerPhone: '*******567',
    rescheduleCount: 0,
    canCancel: true,
    canReschedule: true,
    createdAtUtc: '2026-11-02T07:00:00+00:00',
    cancelledAtUtc: null,
    ...overrides,
  };
}

export function aMessage(overrides: Partial<BookingMessage> = {}): BookingMessage {
  return {
    kind: 'Confirmation',
    status: 'Sent',
    channel: 'Simulated',
    createdAtUtc: '2026-11-02T07:00:00+00:00',
    sentAtUtc: '2026-11-02T07:00:10+00:00',
    subject: 'Your branch appointment is confirmed',
    body: 'Hello Thandi Nkosi,\n\nYour appointment is confirmed.',
    ...overrides,
  };
}
