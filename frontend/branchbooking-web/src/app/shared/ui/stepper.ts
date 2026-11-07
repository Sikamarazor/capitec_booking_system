import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Progress through a short flow: the current step in words, and a bar of segments. */
@Component({
  selector: 'bb-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="Booking progress">
      <p class="text-sm font-semibold text-brand-700">
        Step {{ current() + 1 }} of {{ steps().length }}: {{ steps()[current()] }}
      </p>
      <ol class="mt-2 flex gap-2">
        @for (step of steps(); track step; let i = $index) {
          <li
            class="h-2 flex-1 rounded-full"
            [class.bg-brand-600]="i <= current()"
            [class.bg-line]="i > current()"
            [attr.aria-current]="i === current() ? 'step' : null"
          >
            <span class="sr-only">{{ step }}{{ i < current() ? ' (completed)' : '' }}</span>
          </li>
        }
      </ol>
    </nav>
  `,
})
export class Stepper {
  readonly steps = input.required<string[]>();
  /** Zero-based index of the current step. */
  readonly current = input.required<number>();
}
