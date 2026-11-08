import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Offers generated text to the browser as a file download. Nothing is uploaded and nothing is stored. */
@Injectable({ providedIn: 'root' })
export class FileDownloadService {
  private readonly doc = inject(DOCUMENT);

  save(fileName: string, content: string, mimeType: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: mimeType }));
    const link = this.doc.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    this.doc.body.appendChild(link);
    link.click();
    link.remove();
    // Revoke after the browser has started the download.
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
}
