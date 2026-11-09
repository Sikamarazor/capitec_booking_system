import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Branch } from '../../../core/api/api.models';
import { BranchBookingApiService } from '../../../core/api/branch-booking-api.service';
import { ErrorDescription, describeError } from '../../../core/api/error-messages';
import { Alert } from '../../../shared/ui/alert';
import { Loading } from '../../../shared/ui/loading';
import { BookingDraftService } from '../booking-draft.service';

@Component({
  selector: 'bb-branch-step',
  imports: [Alert, Loading],
  templateUrl: './branch-step.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BranchStep {
  private readonly api = inject(BranchBookingApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly draft = inject(BookingDraftService);

  readonly done = output<void>();
  readonly back = output<void>();

  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly branches = signal<Branch[]>([]);
  protected readonly error = signal<ErrorDescription | null>(null);
  protected readonly query = signal('');
  protected readonly missing = signal(false);

  protected readonly filtered = computed(() => {
    const needle = this.query().trim().toLowerCase();
    return needle
      ? this.branches().filter((b) =>
          [b.name, b.city, b.province, b.addressLine].some((value) => value.toLowerCase().includes(needle)),
        )
      : this.branches();
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.state.set('loading');
    this.api
      .getBranches()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (branches) => {
          this.branches.set(branches);
          this.state.set('ready');
        },
        error: (error: unknown) => {
          this.error.set(describeError(error));
          this.state.set('error');
        },
      });
  }

  protected isSelected(branch: Branch): boolean {
    return this.draft.branch()?.id === branch.id;
  }

  protected select(branch: Branch): void {
    this.draft.chooseBranch(branch);
    this.missing.set(false);
  }

  protected setQuery(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (!this.draft.branch()) {
      this.missing.set(true);
      return;
    }
    this.done.emit();
  }
}
