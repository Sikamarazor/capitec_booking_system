import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith } from 'rxjs';
import { ApiHealthService } from '../../core/http/api-health.service';

type ApiState = 'checking' | 'up' | 'down';

/** A small indicator of whether the booking service answers (the health check from WI-103). */
@Component({
  selector: 'bb-api-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p role="status" class="flex items-center gap-2 text-sm">
      <span class="text-muted">Booking service</span>
      @switch (state()) {
        @case ('up') {
          <span class="chip-success">Online</span>
        }
        @case ('down') {
          <span class="chip-danger">Unavailable</span>
        }
        @default {
          <span class="chip-neutral">Checking</span>
        }
      }
    </p>
  `,
})
export class ApiStatus {
  private readonly health = inject(ApiHealthService);

  protected readonly state = toSignal(
    this.health.getHealth().pipe(
      map((): ApiState => 'up'),
      catchError(() => of<ApiState>('down')),
      startWith<ApiState>('checking'),
    ),
    { requireSync: true },
  );
}
