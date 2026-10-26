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
  // A snackbar fading out is captured with its surface still half-transparent, which fails axe on colours
  // that pass at rest. Wait until no snackbar is left (the text assertion before this already saw it).
  await expect(page.locator('.mat-mdc-snack-bar-label')).toHaveCount(0);
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(
    results.violations,
    results.violations.map((v) => `${v.id}: ${v.help}`).join('\n'),
  ).toEqual([]);
}

async function signIn(page: Page, username = 'admin', password = PASSWORD): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
}

async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
}

async function openHealth(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: 'Health', exact: true }).click();
  await page.getByRole('menuitem', { name, exact: true }).click();
}

/** The SPA's antiforgery token, echoed the same way Angular's XSRF support does. */
async function xsrf(page: Page): Promise<string> {
  const token = (await page.context().cookies()).find((c) => c.name === 'XSRF-TOKEN')?.value;
  return token ? decodeURIComponent(token) : '';
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

test('forgot-password page confirms without revealing accounts', async ({ page }) => {
  await page.goto('/login');
  await page.getByRole('link', { name: 'Forgot your password?' }).click();
  await expect(page.getByRole('heading', { name: 'Reset your password' })).toBeVisible();
  await expectAccessible(page);

  await page.getByLabel('Email').fill('nobody@example.com');
  await page.getByRole('button', { name: 'Send reset link' }).click();
  await expect(page.getByText('If an account with that email exists')).toBeVisible();
  await expectAccessible(page);

  // A link without a token says so instead of showing a form that cannot work.
  await page.goto('/reset-password');
  await expect(page.getByText('This password reset link is invalid or has expired.')).toBeVisible();
  await expectAccessible(page);
});

test('upload an activity, view it, and every page is accessible', async ({ page }) => {
  await signIn(page);
  await page
    .getByLabel('Activity file')
    .setInputFiles({ name: 'run.gpx', mimeType: 'application/gpx+xml', buffer: gpx() });
  await expect(page.getByText('Activity uploaded')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'E2E run' }).first().click();
  await expect(page.getByRole('heading', { name: 'E2E run', level: 1 })).toBeVisible();
  const visibility = page.waitForResponse(
    (r) => /\/activities\/\d+$/.test(r.url()) && r.request().method() === 'PUT',
  );
  await page.getByRole('combobox', { name: 'Visibility' }).click();
  await page.getByRole('option', { name: 'Public' }).click();
  expect((await visibility).ok()).toBe(true);
  await expect(page.getByRole('region', { name: 'Route map' })).toBeVisible();
  await expect(page.getByRole('img', { name: /Heart rate/ })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'Statistics' }).click();
  await expect(page.getByRole('heading', { name: 'Statistics', level: 1 })).toBeVisible();
  await expect(page.getByRole('radiogroup', { name: 'Period' })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: /Administrator/ }).click();
  await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Hide heart rate' })).toBeVisible();
  await page.getByRole('checkbox', { name: 'Hide heart rate' }).check();
  await page.getByRole('button', { name: 'Save privacy' }).click();
  await expect(page.getByText('Privacy saved')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'Users' }).click();
  await expect(page.getByRole('heading', { name: 'Users' })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'Server settings' }).click();
  await expect(page.getByRole('heading', { name: 'Server settings', level: 1 })).toBeVisible();
  await expectAccessible(page);
});

test('add gear, give it a component, attach it to an activity; save the profile', async ({
  page,
}) => {
  await signIn(page);
  const nickname = `E2E bike ${Date.now()}`;

  await page.getByRole('link', { name: 'Gear', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Gear', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Add gear' }).click();
  await page.getByLabel('Nickname', { exact: true }).fill(nickname);
  await page.getByRole('combobox', { name: 'Type' }).first().click();
  await page.getByRole('option', { name: 'Bike' }).click();
  await page.getByRole('button', { name: 'Save gear' }).click();
  await expect(page.getByText('Gear added')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: nickname }).click();
  await expect(page.getByRole('heading', { name: nickname, level: 1 })).toBeVisible();
  await page.getByRole('combobox', { name: 'Component' }).click();
  await page.getByRole('option').nth(1).click();
  await page.getByLabel('Bought on').fill('2026-01-01');
  await page.getByRole('button', { name: 'Save component' }).click();
  await expect(page.getByText('Component saved')).toBeVisible();
  await expectAccessible(page);

  await page.goto('/activities');
  await page.getByRole('link', { name: 'E2E run' }).first().click();
  const saved = page.waitForResponse(
    (r) => r.url().endsWith('/gear') && r.request().method() === 'PUT',
  );
  await page.getByRole('combobox', { name: 'Gear' }).click();
  await page.getByRole('option', { name: nickname }).click();
  expect((await saved).ok()).toBe(true);
  await page.reload();
  await expect(page.getByRole('combobox', { name: 'Gear' })).toContainText(nickname);

  await page.getByRole('link', { name: /Administrator/ }).click();
  await page.getByRole('combobox', { name: 'Currency' }).click();
  await page.getByRole('option', { name: 'Pound' }).click();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Profile saved')).toBeVisible();
  await page.goto('/gear'); // the save must not have signed the user out
  await expect(page.getByRole('heading', { name: 'Gear', level: 1 })).toBeVisible();
});

test('log a weight reading, replace it the same day, delete it', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Weight');
  await expect(page.getByRole('heading', { name: 'Weight', level: 1 })).toBeVisible();

  const save = async (kg: string) => {
    await page.getByLabel('Date', { exact: true }).fill('2026-02-14');
    await page.getByLabel('Weight (kg)').fill(kg);
    await page.getByRole('button', { name: 'Save reading' }).click();
    await expect(page.getByText('Reading saved')).toBeVisible();
  };
  await save('80.5');
  await save('79.9');
  await expect(page.getByRole('row', { name: /Feb 14, 2026/ })).toHaveCount(1);
  await expect(page.getByRole('cell', { name: '79.9 kg' })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the reading from 2026-02-14/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 14, 2026/ })).toHaveCount(0);
});

test('log steps, replace the count the same day, delete it', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Steps');
  await expect(page.getByRole('heading', { name: 'Steps', level: 1 })).toBeVisible();

  const save = async (steps: string) => {
    await page.getByLabel('Date', { exact: true }).fill('2026-02-15');
    await page.getByLabel('Steps', { exact: true }).fill(steps);
    await page.getByRole('button', { name: 'Save steps' }).click();
    await expect(page.getByText('Steps saved')).toBeVisible();
  };
  await save('8000');
  await save('9000');
  await expect(page.getByRole('row', { name: /Feb 15, 2026/ })).toHaveCount(1);
  await expect(page.getByRole('cell', { name: '9000', exact: true })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the steps from 2026-02-15/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 15, 2026/ })).toHaveCount(0);
});

test('log a sleep record, see the computed score, delete it', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Sleep');
  await expect(page.getByRole('heading', { name: 'Sleep', level: 1 })).toBeVisible();

  await page.getByLabel('Date', { exact: true }).fill('2026-02-16');
  await page.getByLabel('Total sleep (hours)').fill('8');
  await page.getByLabel('Deep sleep (minutes)').fill('90');
  await page.getByLabel('Light sleep (minutes)').fill('240');
  await page.getByLabel('REM sleep (minutes)').fill('120');
  await page.getByLabel('Awake (minutes)').fill('10');
  await page.getByLabel('Awakenings').fill('1');
  await page.getByLabel('Average stress').fill('20');
  await page.getByRole('button', { name: 'Save sleep' }).click();
  await expect(page.getByText('Sleep saved')).toBeVisible();
  await expect(page.getByRole('row', { name: /Feb 16, 2026/ })).toHaveCount(1);
  await expect(page.getByRole('cell', { name: /95/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: /Excellent/ })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the sleep record from 2026-02-16/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 16, 2026/ })).toHaveCount(0);
});

test('add water twice, see the daily total, delete it', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Water');
  await expect(page.getByRole('heading', { name: 'Water', level: 1 })).toBeVisible();

  const add = async (ml: string) => {
    await page.getByLabel('Date', { exact: true }).fill('2026-02-17');
    await page.getByLabel('Amount (ml)').fill(ml);
    await page.getByRole('button', { name: 'Add water' }).click();
    await expect(page.getByText('Water added')).toBeVisible();
  };
  await add('250');
  await add('250');
  await expect(page.getByRole('row', { name: /Feb 17, 2026/ })).toHaveCount(1);
  await expect(page.getByRole('cell', { name: /500/ })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the water from 2026-02-17/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 17, 2026/ })).toHaveCount(0);
});

test('save health targets and keep them after reload', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Targets');
  await expect(page.getByRole('heading', { name: 'Health targets', level: 1 })).toBeVisible();
  await page.getByLabel('Steps target').fill('10000');
  await page.getByLabel('Sleep target (hours)').fill('8');
  await page.getByLabel('Water target (ml)').fill('2000');
  await page.getByRole('button', { name: 'Save targets' }).click();
  await expect(page.getByText('Targets saved')).toBeVisible();
  await expectAccessible(page);

  await page.reload();
  await expect(page.getByLabel('Steps target')).toHaveValue('10000');
  await expect(page.getByLabel('Sleep target (hours)')).toHaveValue('8');
  await expect(page.getByLabel('Water target (ml)')).toHaveValue('2000');
});

test('start and end a fast, then delete it', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Fasting');
  await expect(page.getByRole('heading', { name: 'Fasting', level: 1 })).toBeVisible();

  await page.getByLabel('Start time').fill('2026-02-18T20:00');
  await page.getByRole('button', { name: 'Start fast' }).click();
  await expect(page.getByText('Fast started')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Fasting in progress' })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: 'End fast' }).click();
  await expect(page.getByText('Fast ended')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Fasting in progress' })).toHaveCount(0);
  await expect(page.getByRole('row', { name: /Feb 18, 2026/ })).toHaveCount(1);
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the fast from/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 18, 2026/ })).toHaveCount(0);
});

test('log and delete a bowel movement', async ({ page }) => {
  await signIn(page);
  await openHealth(page, 'Bowel movements');
  await expect(page.getByRole('heading', { name: 'Bowel movements', level: 1 })).toBeVisible();

  await page.getByLabel('Date and time').fill('2026-02-19T08:00');
  await page.getByLabel('Notes').fill('e2e');
  await page.getByRole('button', { name: 'Save record' }).click();
  await expect(page.getByText('Bowel movement saved')).toBeVisible();
  await expect(page.getByRole('row', { name: /Feb 19, 2026/ })).toHaveCount(1);
  await expect(page.getByRole('cell', { name: /Smooth soft sausage/ })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Brown', exact: true })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: /Delete the bowel movement from/ }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByRole('row', { name: /Feb 19, 2026/ })).toHaveCount(0);
});

test('an admin can enable sign-up and a new account can sign in', async ({ page }) => {
  const username = `e2e.signup${Date.now()}`;

  await signIn(page);
  await page.getByRole('link', { name: 'Server settings' }).click();
  await page.getByRole('checkbox', { name: 'Allow self sign-up' }).check();
  await page.getByRole('button', { name: 'Save settings' }).click();
  await expect(page.getByText('Server settings saved')).toBeVisible();
  await expectAccessible(page);
  await signOut(page);

  await page.getByRole('link', { name: 'Create an account' }).click();
  await expect(page.getByRole('heading', { name: 'Create an account' })).toBeVisible();
  await page.getByLabel('Name', { exact: true }).fill('E2E Signup');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Email').fill(`${username}@example.com`);
  await page.getByLabel('Password', { exact: true }).fill('Signup-Pass-1!');
  await page.getByLabel('Repeat password').fill('Signup-Pass-1!');
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByText('Account created. You can sign in now.')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('link', { name: 'Back to sign in' }).click();
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill('Signup-Pass-1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
});

test('follow a user, accept the request, and see their public activity in the feed', async ({
  page,
}) => {
  const suffix = Date.now();
  const username = `e2e.fan${suffix}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const headers = { 'X-XSRF-TOKEN': await xsrf(page) };

  const created = await page.request.post('/api/v1/users', {
    headers,
    data: {
      name: 'E2E Fan',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);

  const upload = await page.request.post('/api/v1/activities/upload', {
    headers,
    multipart: { file: { name: 'feed.gpx', mimeType: 'application/gpx+xml', buffer: gpx() } },
  });
  expect(upload.ok()).toBe(true);
  const [activity] = await upload.json();
  const title = `E2E feed run ${suffix}`;
  const visibility = await page.request.fetch(`/api/v1/activities/${activity.id}`, {
    method: 'PUT',
    headers,
    data: { name: title, description: null, activity_type: activity.activity_type, visibility: 0 },
  });
  expect(visibility.ok()).toBe(true);

  await signOut(page);
  await signIn(page, username, password);
  await page.getByRole('link', { name: 'People', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'People', level: 1 })).toBeVisible();
  await expectAccessible(page);
  await page.getByRole('link', { name: 'Administrator' }).first().click();
  await expect(page.getByRole('heading', { name: 'Administrator', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Follow', exact: true }).click();
  await expect(page.getByText('Follow request sent')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Requested', exact: true })).toBeDisabled();
  await expectAccessible(page);

  await signOut(page);
  await signIn(page);
  await page.getByRole('link', { name: /^Notifications/ }).click();
  await expect(page.getByRole('heading', { name: 'Notifications', level: 1 })).toBeVisible();
  await expect(page.getByRole('link', { name: 'E2E Fan wants to follow you.' })).toBeVisible();
  await expectAccessible(page);
  await page.getByRole('link', { name: 'People', exact: true }).click();
  const accept = page.getByRole('button', { name: 'Accept request from E2E Fan' });
  await expect(accept).toBeVisible();
  await accept.click();
  await expect(page.getByText('Request accepted')).toBeVisible();
  await expect(accept).toHaveCount(0);

  await signOut(page);
  await signIn(page, username, password);
  await page.getByRole('link', { name: /^Notifications/ }).click();
  await expect(page.getByRole('heading', { name: 'Notifications', level: 1 })).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Administrator accepted your follow request.' }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Mark all as read' }).click();
  await expect(page.getByRole('button', { name: 'Mark all as read' })).toBeDisabled();
  await expectAccessible(page);
  await page.getByRole('link', { name: 'People', exact: true }).click();
  await page.getByRole('link', { name: 'Administrator' }).first().click();
  await expect(page.getByRole('button', { name: 'Unfollow', exact: true })).toBeVisible();
  await expectAccessible(page);
  await page.getByRole('link', { name: 'Activities', exact: true }).click();
  await page.getByRole('radio', { name: 'Following', exact: true }).click();
  await expect(page.getByRole('link', { name: title })).toBeVisible();
  await expectAccessible(page);
});

test('sign out ends the session', async ({ page }) => {
  await signIn(page);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/activities');
  await expect(page).toHaveURL(/\/login$/);
});
