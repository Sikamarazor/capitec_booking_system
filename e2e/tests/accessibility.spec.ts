import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function expectNoViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(TAGS).analyze();
  const summary = results.violations.map(
    (violation) => `${violation.id} (${violation.impact}): ${violation.help} [${violation.nodes.map((n) => n.target.join(' ')).join(' | ')}]`,
  );
  expect(summary).toEqual([]);
}

test.describe('accessibility (axe, WCAG 2.2 AA, in a real browser)', () => {
  test('home page', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectNoViolations(page);
  });

  test('booking wizard, first step', async ({ page }) => {
    await page.goto('/book');
    await expect(page.getByRole('radio').first()).toBeVisible();
    await expectNoViolations(page);
  });

  test('manage booking, lookup form with an error showing', async ({ page }) => {
    await page.goto('/manage');
    await page.getByRole('button', { name: 'Find my booking' }).click();
    await expect(page.getByText('Enter your booking reference.').first()).toBeVisible();
    await expectNoViolations(page);
  });

  test('page not found', async ({ page }) => {
    await page.goto('/no-such-page');
    await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
    await expectNoViolations(page);
  });
});
