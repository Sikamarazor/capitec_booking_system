import * as axe from 'axe-core';

export async function expectNoA11yViolations(element: HTMLElement): Promise<void> {
  const results = await axe.run(element, {
    rules: { 'color-contrast': { enabled: false }, region: { enabled: false } },
  });

  const summary = results.violations.map(
    (violation) => `${violation.id}: ${violation.help} [${violation.nodes.map((n) => n.target.join(' ')).join(', ')}]`,
  );

  if (summary.length > 0) {
    throw new Error(`Accessibility violations found:\n${summary.join('\n')}`);
  }
}