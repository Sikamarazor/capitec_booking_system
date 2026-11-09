import { Injectable } from '@angular/core';
import { copyText } from './copy-text';

/** Injectable wrapper so components can be tested without touching the real clipboard. */
@Injectable({ providedIn: 'root' })
export class ClipboardService {
  copy(text: string): Promise<boolean> {
    return copyText(text);
  }
}
