import { expect, test } from '@playwright/test';

test.describe('smoke', () => {
  test('the home page loads and the footer says the service is online', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1, name: 'Book a branch appointment' })).toBeVisible();
    await expect(page.getByText('Online', { exact: true })).toBeVisible();
  });

  test('the API answers through the web server', async ({ request }) => {
    const live = await request.get('/api/v1/health');
    expect(live.status()).toBe(200);
    expect((await live.json()).status).toBe('ok');

    expect((await request.get('/api/v1/health/ready')).status()).toBe(200);
  });

  test('the web server sends the security headers', async ({ request }) => {
    const response = await request.get('/');
    const headers = response.headers();

    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['x-frame-options']).toBe('DENY');
    expect(headers['content-security-policy']).toContain("default-src 'self'");
  });

  test('an unknown address shows the not-found page with its own title', async ({ page }) => {
    await page.goto('/no-such-page');

    await expect(page.getByRole('heading', { level: 1, name: 'Page not found' })).toBeVisible();
    await expect(page).toHaveTitle(/Page not found/);
  });

  test('looking up a reference that does not exist gives a friendly message', async ({ page }) => {
    await page.goto('/manage');

    await page.getByLabel('Booking reference').fill('BK-AAAAA-AAAAA');
    await page.getByRole('button', { name: 'Find my booking' }).click();

    await expect(page.getByText('We could not find that booking')).toBeVisible();
  });

  test('the main pages load without console errors or Content-Security-Policy violations', async ({ page }) => {
    const problems: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error') {
        problems.push(message.text());
      }
    });
    page.on('pageerror', (error) => problems.push(error.message));

    for (const path of ['/', '/book', '/manage', '/confirmation']) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    }

    expect(problems).toEqual([]);
  });
});
