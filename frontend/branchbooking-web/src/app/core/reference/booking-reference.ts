const ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

/**
 * Reads what a customer typed as a booking reference the way the API does (case, spaces and hyphens ignored,
 * `O` read as zero, `I` and `L` as one) and returns the canonical `BK-XXXXX-XXXXX`, or `null` when it cannot be one.
 */
export function normaliseReference(input: string): string | null {
  const cleaned = input.toUpperCase().replace(/[\s-]/g, '').replace(/O/g, '0').replace(/[IL]/g, '1');

  if (cleaned.length !== 12 || !cleaned.startsWith('BK')) {
    return null;
  }

  const code = cleaned.slice(2);
  if (![...code].every((c) => ALPHABET.includes(c))) {
    return null;
  }

  return `BK-${code.slice(0, 5)}-${code.slice(5)}`;
}
