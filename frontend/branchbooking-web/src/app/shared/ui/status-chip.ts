import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type ChipTone = 'success' | 'danger' | 'warning' | 'neutral';

const CLASSES: Record<ChipTone, string> = {
  success: 'chip-success',
  danger: 'chip-danger',
  warning: 'chip-warning',
  neutral: 'chip-neutral',
};

@Component({
  selector: 'bb-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="classes()"><ng-content /></span>`,
})
export class StatusChip {
  readonly tone = input<ChipTone>('neutral');
  protected readonly classes = computed(() => CLASSES[this.tone()]);
}
