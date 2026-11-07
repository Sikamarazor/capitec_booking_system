import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A label, optional hint and optional error around one control. The control is projected and must carry
 * `[id]="controlId"`, `[attr.aria-describedby]="describedBy(controlId, hasHint, hasError)"` and
 * `[attr.aria-invalid]="hasError"` (see the booking details step for the pattern).
 */
@Component({
  selector: 'bb-form-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-1.5">
      <label [attr.for]="controlId()" class="block text-sm font-semibold text-ink">{{ label() }}</label>
      @if (hint(); as text) {
        <p [id]="controlId() + '-hint'" class="text-sm text-muted">{{ text }}</p>
      }
      <ng-content />
      @if (error(); as text) {
        <p [id]="controlId() + '-error'" class="text-sm font-medium text-accent-600">{{ text }}</p>
      }
    </div>
  `,
})
export class FormField {
  readonly label = input.required<string>();
  readonly controlId = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly error = input<string | null>(null);
}
