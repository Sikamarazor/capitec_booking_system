import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClipboardService } from '../../core/clipboard/clipboard.service';
import { FileDownloadService } from '../../core/download/file-download.service';
import { ICS_FILE_NAME, ICS_MIME_TYPE, buildIcs } from '../../core/ics/ics';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { BookingSummary } from '../../shared/booking/booking-summary';
import { Alert } from '../../shared/ui/alert';

type CopyState = 'idle' | 'copied' | 'failed';

@Component({
  selector: 'bb-confirmation',
  imports: [RouterLink, Alert, BookingSummary],
  templateUrl: './confirmation.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Confirmation {
  private readonly clipboard = inject(ClipboardService);
  private readonly downloads = inject(FileDownloadService);
  private readonly doc = inject(DOCUMENT);

  protected readonly booking = inject(BookingSessionService).booking;
  protected readonly copyState = signal<CopyState>('idle');

  protected async copyReference(): Promise<void> {
    const booking = this.booking();
    if (!booking) {
      return;
    }

    this.copyState.set((await this.clipboard.copy(booking.reference)) ? 'copied' : 'failed');
  }

  protected addToCalendar(): void {
    const booking = this.booking();
    if (booking) {
      this.downloads.save(ICS_FILE_NAME, buildIcs(booking), ICS_MIME_TYPE);
    }
  }

  protected print(): void {
    this.doc.defaultView?.print();
  }
}
