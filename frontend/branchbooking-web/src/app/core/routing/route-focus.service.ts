import { DestroyRef, Injectable, Injector, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/**
 * After each navigation (except the first page load) moves focus to the page's `h1`, so keyboard and screen-reader
 * users start at the top of the new page instead of on a link that no longer exists.
 */
@Injectable({ providedIn: 'root' })
export class RouteFocusService {
  private readonly injector = inject(Injector);
  private firstNavigation = true;

  constructor() {
    inject(Router)
      .events.pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(() => {
        if (this.firstNavigation) {
          this.firstNavigation = false;
          return;
        }

        afterNextRender(() => this.focusHeading(), { injector: this.injector });
      });
  }

  focusHeading(): void {
    const heading = document.querySelector<HTMLElement>('main h1');
    if (!heading) {
      return;
    }

    if (!heading.hasAttribute('tabindex')) {
      heading.setAttribute('tabindex', '-1');
    }

    heading.focus();
  }
}
