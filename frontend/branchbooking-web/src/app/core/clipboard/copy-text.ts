/**
 * Copies text. Tries the asynchronous Clipboard API, then a hidden text area with `execCommand('copy')` (older
 * browsers, and pages the Clipboard API refuses). Returns whether either worked. The text is never logged and the
 * fallback text area is removed, and focus restored, before this returns.
 */
export async function copyText(
  text: string,
  clipboard: Pick<Clipboard, 'writeText'> | null = globalThis.navigator?.clipboard ?? null,
  doc: Document = document,
): Promise<boolean> {
  if (clipboard) {
    try {
      await clipboard.writeText(text);
      return true;
    } catch {
      // Permission denied or not a secure context: fall through to the fallback.
    }
  }

  const previouslyFocused = doc.activeElement as HTMLElement | null;
  const area = doc.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.setAttribute('aria-hidden', 'true');
  area.style.position = 'fixed';
  area.style.top = '0';
  area.style.left = '0';
  area.style.opacity = '0';
  doc.body.appendChild(area);

  try {
    area.select();
    area.setSelectionRange(0, text.length);
    return doc.execCommand('copy');
  } catch {
    return false;
  } finally {
    area.remove();
    previouslyFocused?.focus?.();
  }
}
