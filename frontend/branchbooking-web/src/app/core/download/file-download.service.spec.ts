import { TestBed } from '@angular/core/testing';
import { FileDownloadService } from './file-download.service';

describe('FileDownloadService', () => {
  const realCreate = URL.createObjectURL;
  const realRevoke = URL.revokeObjectURL;
  let blobs: Blob[];
  let revoked: string[];
  let clicked: { download: string; href: string }[];
  const stopNavigation = (event: Event): void => {
    const link = event.target as HTMLAnchorElement;
    clicked.push({ download: link.download, href: link.href });
    event.preventDefault();
  };

  beforeEach(() => {
    blobs = [];
    revoked = [];
    clicked = [];
    URL.createObjectURL = (blob: Blob | MediaSource) => {
      blobs.push(blob as Blob);
      return 'blob:test-url';
    };
    URL.revokeObjectURL = (url: string) => void revoked.push(url);
    document.addEventListener('click', stopNavigation, true);
  });

  afterEach(() => {
    URL.createObjectURL = realCreate;
    URL.revokeObjectURL = realRevoke;
    document.removeEventListener('click', stopNavigation, true);
  });

  it('offers the content as a named file of the given type and cleans up the link', async () => {
    TestBed.inject(FileDownloadService).save('appointment.ics', 'BEGIN:VCALENDAR', 'text/calendar');

    expect(clicked.length).toBe(1);
    expect(clicked[0].download).toBe('appointment.ics');
    expect(blobs[0].type).toBe('text/calendar');
    expect(await blobs[0].text()).toBe('BEGIN:VCALENDAR');
    expect(document.querySelector('a[download]')).toBeNull();
  });

  it('releases the object URL afterwards', async () => {
    TestBed.inject(FileDownloadService).save('a.txt', 'x', 'text/plain');
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(revoked).toEqual(['blob:test-url']);
  });
});
