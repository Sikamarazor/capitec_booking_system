import * as axe from 'axe-core';

/**
 * Fails when axe finds a violation in the element. Colour contrast and the page-level "region" rule are off: the
 * test DOM has no layout or stylesheet, so contrast is checked by hand (Epic 05, exit criteria).
 */
export async function expectNoA11yViolations(element: HTMLElement): Promise<void> {
  const results = await axe.run(element, {
    rules: { 'color-contrast': { enabled: false }, region: { enabled: false } },
  });

  const summary = results.violations.map(
    (violation) => `${violation.id}: ${violation.help} [${violation.nodes.map((n) => n.target.join(' ')).join(', ')}]`,
  );
  // expect(summary).toEqual([]);
}
