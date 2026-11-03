import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { ApiHealthService } from './core/http/api-health.service';

type ApiState =
  | { kind: 'checking' }
  | { kind: 'up'; version: string }
  | { kind: 'down' };

const checking: ApiState = { kind: 'checking' };

@Component({
  imports: [RouterOutlet],
  selector: 'bb-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly health = inject(ApiHealthService);

  protected readonly apiState = toSignal(
    this.health.getHealth().pipe(
      map((h): ApiState => ({ kind: 'up', version: h.version })),
      catchError(() => of<ApiState>({ kind: 'down' })),
      startWith(checking),
    ),
    { requireSync: true },
  );
}
