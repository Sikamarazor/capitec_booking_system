import { expect, test } from '@playwright/test';
import {
  aCustomer,
  cancelThroughApi,
  chooseFirstAvailableSlot,
  chooseTypeBranchAndTime,
  fillDetails,
  readReference,
  watchForLeaks,
} from './support/booking-flow';

test.describe('the booking journey', () => {
  test('a customer books, looks the booking up, moves it and cancels it', async ({ page }) => {
    const leaks = watchForLeaks(page);
    const customer = aCustomer();

    // 1. Book.
    const chosenTime = await chooseTypeBranchAndTime(page);
    await fillDetails(page, customer);
    await page.getByRole('button', { name: 'Confirm appointment' }).click();

    // 2. Confirmation: the reference, and the time on the branch's clock (not Auckland's).
    await expect(page.getByRole('heading', { level: 1, name: 'Your appointment is booked' })).toBeVisible();
    const reference = await readReference(page);
    await expect(page.getByText(`${chosenTime} to`)).toBeVisible();

    // 3. The calendar file.
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Add to calendar' }).click(),
    ]);
    expect(download.suggestedFilename()).toBe('appointment.ics');
    const { createReadStream } = await import('node:fs');
    const chunks: Buffer[] = [];
    for await (const chunk of createReadStream((await download.path()) as string)) {
      chunks.push(chunk as Buffer);
    }
    const ics = Buffer.concat(chunks).toString('utf8');
    expect(ics).toContain('BEGIN:VCALENDAR');
    expect(ics).toMatch(/DTSTART:\d{8}T\d{6}Z/);

    // 4. A fresh page load forgets the booking; look it up by typing the reference the way a person might.
    await page.goto('/manage');
    await expect(page.getByRole('heading', { name: 'Find your booking' })).toBeVisible();
    await page.getByLabel('Booking reference').fill(reference.toLowerCase().replace(/-/g, ' '));
    await page.getByRole('button', { name: 'Find my booking' }).click();
    await expect(page.getByRole('heading', { name: 'Your appointment', exact: true })).toBeVisible();
    await expect(page.getByText(`${chosenTime} to`)).toBeVisible();
    await expect(page.getByText('@example.com')).toBeVisible();
    await expect(page.getByText(customer.email)).toHaveCount(0); // the API masks it

    // 5. The Worker sends the confirmation (the simulated channel labels it as a demo).
    await expect(async () => {
      await page.getByRole('button', { name: 'Refresh' }).click();
      await expect(page.getByText('Demo message. Nothing was emailed.')).toBeVisible({ timeout: 3_000 });
    }).toPass({ timeout: 60_000 });
    await expect(page.locator('li', { hasText: 'Booking confirmation' })).toBeVisible();

    // 6. Move it.
    await Promise.all([
      page.waitForResponse((r) => r.url().includes('/availability')),
      page.getByRole('button', { name: 'Move appointment' }).click(),
    ]);
    const newTime = await chooseFirstAvailableSlot(page);
    await page.getByRole('button', { name: 'Confirm new time' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Appointment moved' })).toBeVisible();
    await expect(page.getByText(`${newTime} to`)).toBeVisible();
    await expect(async () => {
      await page.getByRole('button', { name: 'Refresh' }).click();
      await expect(page.locator('li', { hasText: 'Appointment moved' })).toBeVisible({ timeout: 3_000 });
    }).toPass({ timeout: 60_000 });

    // 7. Cancel it.
    await page.getByRole('button', { name: 'Cancel appointment' }).click();
    await page.getByLabel('Reason (optional)').fill('End-to-end test');
    await page.getByRole('button', { name: 'Yes, cancel appointment' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Appointment cancelled' })).toBeVisible();
    await expect(page.getByText('This booking is cancelled')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Cancel appointment' })).toHaveCount(0);

    // 8. The reference never appeared anywhere a browser keeps or shows it.
    expect(leaks.urls.filter((url) => url.includes(reference))).toEqual([]);
    expect(leaks.console.filter((text) => text.includes(reference))).toEqual([]);
    expect(await page.title()).not.toContain(reference);
    const stored = await page.evaluate(() => ({
      local: localStorage.length,
      session: sessionStorage.length,
      history: JSON.stringify(history.state ?? null),
    }));
    expect(stored.local).toBe(0);
    expect(stored.session).toBe(0);
    expect(stored.history).not.toContain(reference);
  });

  test('double-clicking Confirm creates exactly one booking', async ({ page, request }) => {
    const created: string[] = [];
    page.on('request', (r) => {
      if (r.method() === 'POST' && new URL(r.url()).pathname === '/api/v1/bookings') {
        created.push(r.url());
      }
    });

    await chooseTypeBranchAndTime(page);
    await fillDetails(page, aCustomer());
    await page.getByRole('button', { name: 'Confirm appointment' }).dblclick();

    await expect(page.getByRole('heading', { level: 1, name: 'Your appointment is booked' })).toBeVisible();
    expect(created.length).toBe(1);

    await cancelThroughApi(request, await readReference(page));
  });
});
