import { APIRequestContext, Page, expect } from '@playwright/test';

export const REFERENCE_PATTERN = /^BK-[0-9A-Z]{5}-[0-9A-Z]{5}$/;

export interface Customer {
  fullName: string;
  email: string;
  phone: string;
}

/** A fresh, obviously fake customer. `example.com` is reserved, so nothing can ever reach a real inbox. */
export function aCustomer(): Customer {
  return { fullName: 'E2E Customer', email: `e2e-${Date.now()}@example.com`, phone: '082 123 4567' };
}

/**
 * On the slot picker, opens future days until one has an open time and chooses the first one. Returns its label
 * (`HH:mm`, the branch's wall clock). Looks at up to three weeks. Waits on the availability response, not on timers.
 * Seeded branches use Africa/Johannesburg. Skip today so the journey's move/cancel steps remain outside
 * the two-hour change cutoff, even when the earliest bookable slot is only an hour away.
 */
export async function chooseFirstAvailableSlot(page: Page): Promise<string> {
  await expect(page.locator('button[data-day]').first()).toBeVisible();
  const branchToday = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Johannesburg', year: 'numeric', month: '2-digit', day: '2-digit',
  }).format(new Date());

  for (let week = 0; week < 3; week++) {
    const days = page.locator('button[data-day]:not([disabled])');
    const count = await days.count();

    for (let index = 0; index < count; index++) {
      const day = days.nth(index);
      const date = await day.getAttribute('data-day');
      if (!date || date <= branchToday) continue;

      if ((await day.getAttribute('aria-pressed')) !== 'true') {
        await Promise.all([
          page.waitForResponse((r) => r.url().includes('/availability') && r.url().includes(`date=${date}`)),
          day.click(),
        ]);
      }

      await expect(page.getByText('Loading times')).toBeHidden();
      const open = page.locator('button[data-slot]:not([disabled])');
      if ((await open.count()) > 0) {
        const slot = open.first();
        const label = (await slot.getAttribute('data-slot')) ?? '';
        await slot.click();
        return label;
      }
    }

    await page.getByRole('button', { name: 'Next week' }).click();
  }

  throw new Error('No open appointment time was found in the next three weeks. Check the seed data and the booking window.');
}

/** Walks the first three wizard steps (first appointment type, first branch, first open time). Returns the time label. */
export async function chooseTypeBranchAndTime(page: Page): Promise<string> {
  await page.goto('/book');
  await expect(page.getByRole('heading', { level: 1, name: 'Book an appointment' })).toBeVisible();

  await page.getByRole('group', { name: 'Appointment type', exact: true }).getByRole('radio').first().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  await page.getByRole('group', { name: 'Branch', exact: true }).getByRole('radio').first().check();
  await page.getByRole('button', { name: 'Continue' }).click();

  const label = await chooseFirstAvailableSlot(page);
  await page.getByRole('button', { name: 'Continue' }).click();
  return label;
}

export async function fillDetails(page: Page, customer: Customer): Promise<void> {
  await page.getByLabel('Full name').fill(customer.fullName);
  await page.getByLabel('Email address').fill(customer.email);
  await page.getByLabel('Phone number').fill(customer.phone);
}

/** Reads the reference from the confirmation page. */
export async function readReference(page: Page): Promise<string> {
  const reference = (await page.locator('h2:has-text("Your booking reference") + p').innerText()).trim();
  expect(reference).toMatch(REFERENCE_PATTERN);
  return reference;
}

/** Cancels a booking through the API so a test never leaves capacity used. Ignores "already cancelled". */
export async function cancelThroughApi(request: APIRequestContext, reference: string): Promise<void> {
  await request.post(`/api/v1/bookings/${encodeURIComponent(reference)}/cancel`, { data: {} });
}

/** Collects what a page leaks: every URL it visits and every console message. */
export function watchForLeaks(page: Page): { urls: string[]; console: string[] } {
  const seen = { urls: [] as string[], console: [] as string[] };
  page.on('framenavigated', (frame) => {
    if (frame === page.mainFrame()) {
      seen.urls.push(frame.url());
    }
  });
  page.on('console', (message) => seen.console.push(message.text()));
  return seen;
}
