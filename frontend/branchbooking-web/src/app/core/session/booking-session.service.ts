import { Injectable, signal } from '@angular/core';
import { Booking } from '../api/api.models';

/**
 * The booking the customer is looking at (just created, or looked up). Held in memory only: the reference is a
 * credential, so it is never written to storage, a URL or the history state (Epic 05, D2). A reload clears it.
 */
@Injectable({ providedIn: 'root' })
export class BookingSessionService {
  private readonly current = signal<Booking | null>(null);

  readonly booking = this.current.asReadonly();

  set(booking: Booking): void {
    this.current.set(booking);
  }

  clear(): void {
    this.current.set(null);
  }
}
