import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type AlertVariant = 'error' | 'warning' | 'success' | 'info';

const STYLES: Record<AlertVariant, string> = {
  error: 'border-accent-600 bg-accent-600/10 text-ink',
  warning: 'border-warning bg-warning/10 text-ink',
  success: 'border-success bg-success/10 text-ink',
  info: 'border-brand-600 bg-brand-50 text-ink',
};

/** A message box. Errors use `role="alert"` (announced at once); everything else uses `role="status"` (announced politely). */
@Component({
  selector: 'bb-alert',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [class]="classes()" [attr.role]="variant() === 'error' ? 'alert' : 'status'">
      @if (heading(); as text) {
        <p class="font-semibold">{{ text }}</p>
      }
      <div class="text-base"><ng-content /></div>
    </div>
  `,
})
export class Alert {
  readonly variant = input<AlertVariant>('info');
  readonly heading = input<string>();

  protected readonly classes = computed(() => `rounded-control border-l-4 px-4 py-3 ${STYLES[this.variant()]}`);
}
