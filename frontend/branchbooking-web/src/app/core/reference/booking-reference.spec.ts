import { normaliseReference } from './booking-reference';

describe('normaliseReference', () => {
  it('accepts the canonical form', () => {
    expect(normaliseReference('BK-7H2QX-M9RTD')).toBe('BK-7H2QX-M9RTD');
  });

  it('ignores case, spaces and hyphens', () => {
    expect(normaliseReference(' bk 7h2qx m9rtd ')).toBe('BK-7H2QX-M9RTD');
    expect(normaliseReference('bk7h2qxm9rtd')).toBe('BK-7H2QX-M9RTD');
  });

  it('reads O as zero and I or L as one, as the API does', () => {
    expect(normaliseReference('BK-OILQX-M9RTD')).toBe('BK-011QX-M9RTD');
  });

  it('rejects a wrong prefix', () => {
    expect(normaliseReference('XX-7H2QX-M9RTD')).toBeNull();
  });

  it('rejects a wrong length', () => {
    expect(normaliseReference('BK-7H2QX-M9RT')).toBeNull();
    expect(normaliseReference('BK-7H2QX-M9RTDD')).toBeNull();
  });

  it('rejects characters outside the alphabet', () => {
    expect(normaliseReference('BK-7H2QU-M9RTD')).toBeNull();
    expect(normaliseReference('BK-7H2Q!-M9RTD')).toBeNull();
  });

  it('rejects empty input', () => {
    expect(normaliseReference('')).toBeNull();
    expect(normaliseReference('   ')).toBeNull();
  });
});
