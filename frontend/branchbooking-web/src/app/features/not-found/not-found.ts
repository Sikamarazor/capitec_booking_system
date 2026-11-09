import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'bb-not-found',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card">
      <h1 class="text-3xl font-bold text-brand-900">Page not found</h1>
      <p class="mt-2 text-muted">The page you were looking for does not exist.</p>
      <a routerLink="/" class="btn-primary mt-6">Go to the home page</a>
    </section>
  `,
})
export class NotFound {}
