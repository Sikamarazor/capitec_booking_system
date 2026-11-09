import { copyText } from './copy-text';

describe('copyText', () => {
  let originalExec: typeof document.execCommand;

  beforeEach(() => {
    originalExec = document.execCommand;
  });

  afterEach(() => {
    document.execCommand = originalExec;
  });

  it('uses the Clipboard API when it works', async () => {
    const written: string[] = [];

    const ok = await copyText('BK-7H2QX-M9RTD', { writeText: async (t: string) => void written.push(t) });

    expect(ok).toBe(true);
    expect(written).toEqual(['BK-7H2QX-M9RTD']);
  });

  it('falls back to a hidden text area when the API refuses, and restores focus', async () => {
    const button = document.createElement('button');
    document.body.appendChild(button);
    button.focus();
    let copied: string | null = null;
    document.execCommand = ((command: string) => {
      copied = (document.querySelector('textarea') as HTMLTextAreaElement).value;
      return command === 'copy';
    }) as typeof document.execCommand;

    const ok = await copyText('BK-7H2QX-M9RTD', {
      writeText: () => Promise.reject(new Error('denied')),
    });

    expect(ok).toBe(true);
    expect(copied).toBe('BK-7H2QX-M9RTD');
    expect(document.querySelector('textarea')).toBeNull();
    expect(document.activeElement).toBe(button);
    button.remove();
  });

  it('falls back when there is no Clipboard API at all', async () => {
    document.execCommand = (() => true) as typeof document.execCommand;

    expect(await copyText('x', null)).toBe(true);
  });

  it('reports failure when both routes fail, and leaves nothing behind', async () => {
    document.execCommand = (() => false) as typeof document.execCommand;

    const ok = await copyText('x', { writeText: () => Promise.reject(new Error('denied')) });

    expect(ok).toBe(false);
    expect(document.querySelector('textarea')).toBeNull();
  });
});
