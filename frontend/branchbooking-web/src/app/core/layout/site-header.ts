import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { BRAND } from '../../core/brand';

@Component({
  selector: 'bb-site-header',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="no-print bg-brand-600 text-white">
      <div class="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-4">
        <a routerLink="/" class="text-xl font-bold tracking-tight text-white focus-visible:outline-white">
          {{ brand.name }} <span class="font-normal">{{ brand.product }}</span>
        </a>
        <nav aria-label="Main">
          <ul class="flex gap-5 text-base font-semibold">
            <li>
              <a
                routerLink="/book"
                routerLinkActive="underline"
                ariaCurrentWhenActive="page"
                class="inline-flex min-h-11 items-center underline-offset-4 hover:underline focus-visible:outline-white"
                >Book</a
              >
            </li>
            <li>
              <a
                routerLink="/manage"
                routerLinkActive="underline"
                ariaCurrentWhenActive="page"
                class="inline-flex min-h-11 items-center underline-offset-4 hover:underline focus-visible:outline-white"
                >Manage booking</a
              >
            </li>
          </ul>
        </nav>
      </div>
    </header>
  `,
})
export class SiteHeader {
  protected readonly brand = BRAND;
}
