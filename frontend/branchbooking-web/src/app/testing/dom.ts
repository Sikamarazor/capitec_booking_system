/** Small helpers for specs that drive a component through its DOM. */
export function query<T extends Element = HTMLElement>(root: ParentNode, selector: string): T | null {
  return root.querySelector<T>(selector);
}

export function click(root: ParentNode, selector: string): void {
  const element = query<HTMLElement>(root, selector);
  if (!element) {
    throw new Error(`Nothing matches ${selector}`);
  }
  element.click();
}

export function setValue(root: ParentNode, selector: string, value: string): void {
  const input = query<HTMLInputElement | HTMLTextAreaElement>(root, selector);
  if (!input) {
    throw new Error(`Nothing matches ${selector}`);
  }
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('blur', { bubbles: true }));
}

export function textOf(root: ParentNode, selector?: string): string {
  const element = selector ? query(root, selector) : (root as Element);
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

export function buttonWithText(root: ParentNode, text: string): HTMLButtonElement | null {
  return Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes(text)) ?? null;
}
