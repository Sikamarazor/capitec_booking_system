import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BRAND } from '../../core/brand';
import { ApiStatus } from './api-status';

@Component({
  selector: 'bb-site-footer',
  imports: [ApiStatus],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="no-print mt-auto border-t border-line bg-card">
      <div class="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-6 text-sm text-muted">
        <p>{{ brand.full }} &middot; demonstration build</p>
        <bb-api-status />
      </div>
    </footer>
  `,
})
export class SiteFooter {
  protected readonly brand = BRAND;
}
