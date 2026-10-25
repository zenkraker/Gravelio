import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';

const PASSWORD = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin-Password-1!';

/** A short run in Lisbon, unique per run so repeated runs don't hit the duplicate check. */
function gpx(): Buffer {
  const start = Date.now() - 86_400_000;
  const points = Array.from({ length: 60 }, (_, i) => {
    const time = new Date(start + i * 10_000).toISOString().replace(/\.\d+Z$/, 'Z');
    return (
      `<trkpt lat="${38.7223 + i * 0.0005}" lon="${-9.1393 + i * 0.0003}"><ele>${100 + i}</ele><time>${time}</time>` +
      `<extensions><hr>${130 + (i % 15)}</hr></extensions></trkpt>`
    );
  });
  return Buffer.from(
    `<gpx xmlns="http://www.topografix.com/GPX/1/1"><trk><name>E2E run</name><type>running</type><trkseg>${points.join('')}</trkseg></trk></gpx>`,
  );
}

async function expectAccessible(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(
    results.violations,
    results.violations.map((v) => `${v.id}: ${v.help}`).join('\n'),
  ).toEqual([]);
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
}

test.beforeEach(async ({ page }) => {
  await page.route('**/tile.openstreetmap.org/**', (route) => route.abort()); // keep tests offline
});

test('login page is accessible and rejects a wrong password', async ({ page }) => {
  await page.goto('/login');
  await expectAccessible(page);
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill('wrong-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toContainText('Incorrect username or password');
  await expectAccessible(page);
});

test('unauthenticated visitors are sent to the login page', async ({ page }) => {
  await page.goto('/activities');
  await expect(page).toHaveURL(/\/login$/);
});

test('upload an activity, view it, and every page is accessible', async ({ page }) => {
  await signIn(page);
  await page
    .locator('input[type=file]')
    .setInputFiles({ name: 'run.gpx', mimeType: 'application/gpx+xml', buffer: gpx() });
  await expect(page.getByText('Activity uploaded')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'E2E run' }).first().click();
  await expect(page.getByRole('heading', { name: 'E2E run' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Route map' })).toBeVisible();
  await expect(page.getByRole('img', { name: /Heart rate/ })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: /Administrator/ }).click();
  await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'Users' }).click();
  await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  await expectAccessible(page);
});

test('sign out ends the session', async ({ page }) => {
  await signIn(page);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/activities');
  await expect(page).toHaveURL(/\/login$/);
});
