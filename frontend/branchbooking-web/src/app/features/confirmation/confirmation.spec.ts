import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ClipboardService } from '../../core/clipboard/clipboard.service';
import { FileDownloadService } from '../../core/download/file-download.service';
import { BookingSessionService } from '../../core/session/booking-session.service';
import { expectNoA11yViolations } from '../../testing/a11y';
import { aBooking } from '../../testing/api-fixtures';
import { buttonWithText, click, textOf } from '../../testing/dom';
import { Confirmation } from './confirmation';

class FakeClipboard {
  copied: string[] = [];
  works = true;
  async copy(text: string): Promise<boolean> {
    this.copied.push(text);
    return this.works;
  }
}

class FakeDownloads {
  saved: { fileName: string; content: string; mimeType: string }[] = [];
  save(fileName: string, content: string, mimeType: string): void {
    this.saved.push({ fileName, content, mimeType });
  }
}

describe('Confirmation', () => {
  let clipboard: FakeClipboard;
  let downloads: FakeDownloads;

  beforeEach(() => {
    clipboard = new FakeClipboard();
    downloads = new FakeDownloads();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ClipboardService, useValue: clipboard },
        { provide: FileDownloadService, useValue: downloads },
      ],
    });
  });

  function render(withBooking = true) {
    if (withBooking) {
      TestBed.inject(BookingSessionService).set(aBooking());
    }
    const fixture = TestBed.createComponent(Confirmation);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('shows the reference prominently with the appointment details', () => {
    const { root } = render();

    expect(textOf(root, 'h1')).toBe('Your appointment is booked');
    expect(textOf(root, 'p.font-mono')).toBe('BK-7H2QX-M9RTD');
    expect(textOf(root)).toContain('Test Branch');
    expect(textOf(root)).toContain('08:00 to 08:30');
    expect(textOf(root)).toContain('t*****@example.com');
  });

  it('copies the reference and says so', async () => {
    const { fixture, root } = render();

    click(root, 'button.btn-secondary');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(clipboard.copied).toEqual(['BK-7H2QX-M9RTD']);
    expect(textOf(root, '[role="status"]')).toContain('Reference copied');
  });

  it('tells the customer to copy by hand when copying fails', async () => {
    clipboard.works = false;
    const { fixture, root } = render();

    buttonWithText(root, 'Copy reference')?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(textOf(root)).toContain('Select the reference above and copy it yourself');
  });

  it('offers the appointment as a calendar file', () => {
    const { root } = render();

    buttonWithText(root, 'Add to calendar')?.click();

    expect(downloads.saved.length).toBe(1);
    expect(downloads.saved[0].fileName).toBe('appointment.ics');
    expect(downloads.saved[0].mimeType).toContain('text/calendar');
    expect(downloads.saved[0].content).toContain('BEGIN:VCALENDAR');
    expect(downloads.saved[0].content).toContain('DTSTART:20261103T060000Z');
  });

  it('explains itself and offers the way forward when there is no booking in memory', () => {
    const { root } = render(false);

    expect(textOf(root, 'h1')).toBe('No booking to show');
    expect(textOf(root)).not.toContain('BK-');
    expect(root.querySelector('a[href="/manage"]')).toBeTruthy();
    expect(root.querySelector('a[href="/book"]')).toBeTruthy();
  });

  it('writes nothing to browser storage', async () => {
    const before = [localStorage.length, sessionStorage.length];
    const { fixture, root } = render();

    buttonWithText(root, 'Copy reference')?.click();
    await fixture.whenStable();

    expect([localStorage.length, sessionStorage.length]).toEqual(before);
  });

  it('has no accessibility violations with a booking', async () => {
    const { root } = render();

    await expectNoA11yViolations(root);
  });

  it('has no accessibility violations without one', async () => {
    const { root } = render(false);

    await expectNoA11yViolations(root);
  });
});
