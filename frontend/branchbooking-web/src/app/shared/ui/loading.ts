import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'bb-loading',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="status" class="flex items-center gap-3 py-4 text-muted">
      <span
        class="size-5 animate-spin rounded-full border-2 border-line border-t-brand-600 motion-reduce:animate-none"
        aria-hidden="true"
      ></span>
      <span>{{ label() }}</span>
    </div>
  `,
})
export class Loading {
  readonly label = input('Loading');
}
