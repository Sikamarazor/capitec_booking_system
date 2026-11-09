import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AppointmentType } from '../../core/api/api.models';
import { BranchBookingApiService } from '../../core/api/branch-booking-api.service';

@Component({
  selector: 'bb-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly api = inject(BranchBookingApiService);

  /** What can be booked. If the service is down the list is simply left out; the buttons still work. */
  protected readonly types = toSignal(
    this.api.getAppointmentTypes().pipe(catchError(() => of<AppointmentType[]>([]))),
    { initialValue: [] as AppointmentType[] },
  );
}
