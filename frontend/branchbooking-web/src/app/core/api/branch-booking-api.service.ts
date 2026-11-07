import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AppointmentType,
  Availability,
  Booking,
  BookingMessage,
  Branch,
  CreateBookingRequest,
} from './api.models';

/** The only code that talks to the booking API. Callers pass references already normalised by `normaliseReference`. */
@Injectable({ providedIn: 'root' })
export class BranchBookingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getAppointmentTypes(): Observable<AppointmentType[]> {
    return this.http.get<AppointmentType[]>(`${this.base}/appointment-types`);
  }

  getBranches(): Observable<Branch[]> {
    return this.http.get<Branch[]>(`${this.base}/branches`);
  }

  getAvailability(branchId: string, date: string): Observable<Availability> {
    return this.http.get<Availability>(`${this.base}/branches/${encodeURIComponent(branchId)}/availability`, {
      params: { date },
    });
  }

  createBooking(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(`${this.base}/bookings`, request);
  }

  getBooking(reference: string): Observable<Booking> {
    return this.http.get<Booking>(this.bookingUrl(reference));
  }

  cancelBooking(reference: string, reason?: string): Observable<Booking> {
    const trimmed = reason?.trim();
    return this.http.post<Booking>(`${this.bookingUrl(reference)}/cancel`, trimmed ? { reason: trimmed } : {});
  }

  rescheduleBooking(reference: string, newStartsAtUtc: string): Observable<Booking> {
    return this.http.post<Booking>(`${this.bookingUrl(reference)}/reschedule`, { newStartsAtUtc });
  }

  getMessages(reference: string): Observable<BookingMessage[]> {
    return this.http.get<BookingMessage[]>(`${this.bookingUrl(reference)}/messages`);
  }

  private bookingUrl(reference: string): string {
    return `${this.base}/bookings/${encodeURIComponent(reference)}`;
  }
}
