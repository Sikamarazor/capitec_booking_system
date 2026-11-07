import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { Alert } from './alert';

export interface ErrorSummaryItem {
  /** The id of the control to focus. */
  id: string;
  message: string;
}

/** Lists what is wrong at the top of a form. Each entry is a button that moves focus to its field. */
@Component({
  selector: 'bb-error-summary',
  imports: [Alert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length > 0) {
      <div #box tabindex="-1" class="outline-none">
        <bb-alert variant="error" heading="Please fix the following">
          <ul class="mt-2 list-disc space-y-1 pl-5">
            @for (item of items(); track item.id) {
              <li>
                <button type="button" class="min-h-6 text-left underline" (click)="focusField(item.id)">
                  {{ item.message }}
                </button>
              </li>
            }
          </ul>
        </bb-alert>
      </div>
    }
  `,
})
export class ErrorSummary {
  readonly items = input<ErrorSummaryItem[]>([]);
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');

  /** Call after setting items so the summary is where the keyboard is. */
  focus(): void {
    this.box()?.nativeElement.focus();
  }

  protected focusField(id: string): void {
    document.getElementById(id)?.focus();
  }
}
