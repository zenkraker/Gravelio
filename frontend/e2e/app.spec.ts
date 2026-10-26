import AxeBuilder from '@axe-core/playwright';
import { createHmac } from 'node:crypto';
import { Page, expect, test } from '@playwright/test';

const PASSWORD = process.env['E2E_ADMIN_PASSWORD'] ?? 'Admin-Password-1!';

/** A short run in Lisbon, unique per run so repeated runs don't hit the duplicate check. */
function gpx(name = 'E2E run', sport = 'running', daysAgo = 1, startMs?: number): Buffer {
  const start = startMs ?? Date.now() - daysAgo * 86_400_000;
  const points = Array.from({ length: 60 }, (_, i) => {
    const time = new Date(start + i * 10_000).toISOString().replace(/\.\d+Z$/, 'Z');
    return (
      `<trkpt lat="${38.7223 + i * 0.0005}" lon="${-9.1393 + i * 0.0003}"><ele>${100 + i}</ele><time>${time}</time>` +
      `<extensions><hr>${130 + (i % 15)}</hr></extensions></trkpt>`
    );
  });
  return Buffer.from(
    `<gpx xmlns="http://www.topografix.com/GPX/1/1"><trk><name>${name}</name><type>${sport}</type><trkseg>${points.join('')}</trkseg></trk></gpx>`,
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

/** RFC 6238 code (30 s step, SHA-1, 6 digits) for an authenticator key, `stepsAhead` steps from now. */
function totp(base32: string, stepsAhead = 0): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  const bits = [...base32.toUpperCase().replace(/[^A-Z2-7]/g, '')]
    .map((c) => alphabet.indexOf(c).toString(2).padStart(5, '0'))
    .join('');
  const key = Buffer.from(bits.match(/.{8}/g)!.map((b) => parseInt(b, 2)));
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000) + stepsAhead));
  const hash = createHmac('sha1', key).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary = hash.readUInt32BE(offset) & 0x7fffffff;
  return String(binary % 1_000_000).padStart(6, '0');
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

/** A TCX run with two laps of 30 points, unique per run. */
function tcxWithLaps(name: string): Buffer {
  const start = Date.now() - 40 * 86_400_000;
  const iso = (ms: number) => new Date(ms).toISOString().replace(/\.\d+Z$/, 'Z');
  const lap = (n: number) => {
    const points = Array.from({ length: 30 }, (_, k) => {
      const i = n * 30 + k;
      return (
        `<Trackpoint><Time>${iso(start + i * 10_000)}</Time><Position><LatitudeDegrees>${38.7223 + i * 0.0005}</LatitudeDegrees>` +
        `<LongitudeDegrees>${-9.1393 + i * 0.0003}</LongitudeDegrees></Position><AltitudeMeters>${100 + i}</AltitudeMeters>` +
        `<HeartRateBpm><Value>${130 + (i % 15)}</Value></HeartRateBpm></Trackpoint>`
      );
    }).join('');
    return (
      `<Lap StartTime="${iso(start + n * 300_000)}"><TotalTimeSeconds>300</TotalTimeSeconds><DistanceMeters>1800</DistanceMeters>` +
      `<AverageHeartRateBpm><Value>${140 + n}</Value></AverageHeartRateBpm><Track>${points}</Track></Lap>`
    );
  };
  return Buffer.from(
    `<TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2"><Activities>` +
      `<Activity Sport="Running"><Id>${name}</Id>${lap(0)}${lap(1)}</Activity></Activities></TrainingCenterDatabase>`,
  );
}

test('an activity recorded in laps shows a laps table', async ({ page }) => {
  await signIn(page);
  await page.goto('/activities');
  await page.getByLabel('Activity file').setInputFiles({
    name: 'laps.tcx',
    mimeType: 'application/vnd.garmin.tcx+xml',
    buffer: tcxWithLaps(`laps-${Date.now()}`),
  });
  await expect(page.getByText('Activity uploaded')).toBeVisible();
  await page
    .getByRole('link', { name: /Running|Run/ })
    .first()
    .click();
  const laps = page.getByRole('table', { name: 'Laps' });
  await expect(laps).toBeVisible();
  await expect(laps.getByRole('row')).toHaveCount(3);
  await expect(laps.getByRole('row').nth(2)).toContainText('141');
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

test('a sign-up that needs approval waits for an admin, who is told and approves it', async ({
  page,
}) => {
  const username = `e2e.pending${Date.now()}`;

  await signIn(page);
  await page.getByRole('link', { name: 'Server settings' }).click();
  await page.getByRole('checkbox', { name: 'Allow self sign-up' }).check();
  await page
    .getByRole('checkbox', { name: 'Require an administrator to approve new accounts' })
    .check();
  await page.getByRole('button', { name: 'Save settings' }).click();
  await expect(page.getByText('Server settings saved')).toBeVisible();
  await signOut(page);

  try {
    await page.getByRole('link', { name: 'Create an account' }).click();
    await page.getByLabel('Name', { exact: true }).fill('E2E Pending');
    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Email').fill(`${username}@example.com`);
    await page.getByLabel('Password', { exact: true }).fill('Signup-Pass-1!');
    await page.getByLabel('Repeat password').fill('Signup-Pass-1!');
    await page.getByRole('button', { name: 'Create account' }).click();
    await page.getByRole('link', { name: 'Back to sign in' }).click();
    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Password').fill('Signup-Pass-1!');
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByText('waiting for approval by an administrator')).toBeVisible();

    await signIn(page);
    await page.getByRole('link', { name: 'Notifications' }).first().click();
    await expect(
      page.getByText(`E2E Pending signed up and is waiting for your approval.`),
    ).toBeVisible();
    await page.getByRole('link', { name: 'Users' }).click();
    await expect(page.getByRole('row', { name: new RegExp(username) })).toContainText(
      'Pending approval',
    );
    await expectAccessible(page);
    await page.getByRole('button', { name: `Approve ${username}` }).click();
    await expect(page.getByRole('row', { name: new RegExp(username) })).toContainText('Active');
    await signOut(page);

    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Password').fill('Signup-Pass-1!');
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
    await signOut(page);
  } finally {
    await signIn(page);
    await page.getByRole('link', { name: 'Server settings' }).click();
    await page
      .getByRole('checkbox', { name: 'Require an administrator to approve new accounts' })
      .uncheck();
    await page.getByRole('button', { name: 'Save settings' }).click();
    await expect(page.getByText('Server settings saved')).toBeVisible();
  }
});

test('see the signed-in devices and end another one', async ({ page, browser }) => {
  await signIn(page);
  const other = await browser.newContext();
  const phone = await other.newPage();
  await signIn(phone);

  await page.goto('/security');
  await expect(page.getByRole('heading', { name: 'Signed-in devices' })).toBeVisible();
  const devices = page.getByRole('table', { name: 'Signed-in devices' });
  await expect(devices.getByText('(this device)')).toBeVisible();
  await expectAccessible(page);

  // Earlier tests leave sessions of the same admin behind, so end every other one; the phone is among them
  // and is signed out at its next request.
  const end = devices.getByRole('button', { name: /^End session/ });
  while ((await end.count()) > 0) {
    const before = await end.count();
    await end.first().click();
    await expect(end).toHaveCount(before - 1);
  }
  await phone.goto('/activities');
  await expect(phone).toHaveURL(/\/login$/);
  await other.close();
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

test('turn on two-factor authentication, sign in with a code, and use a recovery code', async ({
  page,
}) => {
  const suffix = Date.now();
  const username = `e2e.mfa${suffix}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const created = await page.request.post('/api/v1/users', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    data: {
      name: 'E2E Mfa',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);
  await signOut(page);

  await signIn(page, username, password);
  await page.getByRole('link', { name: /E2E Mfa/ }).click();
  await page.getByRole('link', { name: 'Security', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Security', level: 1 })).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: 'Set up' }).click();
  const secret = (await page.locator('code.key').innerText()).replace(/\s/g, '');
  await page.getByLabel('Current password').first().fill(password); // the API key form below has one too
  await page.getByLabel('Code from the app').fill(totp(secret));
  await expectAccessible(page);
  await page.getByRole('button', { name: 'Turn on' }).click();
  await expect(
    page.getByRole('list', { name: 'Recovery codes' }).getByRole('listitem'),
  ).toHaveCount(10);
  const recovery = await page
    .getByRole('list', { name: 'Recovery codes' })
    .getByRole('listitem')
    .first()
    .innerText();
  await expectAccessible(page);
  await page.getByRole('button', { name: 'I have saved my codes' }).click();
  await expect(page.getByText('Recovery codes left: 10')).toBeVisible();

  await signOut(page);
  // The code that turned MFA on is spent, so sign in with one from the next step.
  await page.goto('/login');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByLabel('Code')).toBeVisible();
  await expectAccessible(page);
  await page.getByLabel('Code').fill('000000');
  await page.getByRole('button', { name: 'Verify' }).click();
  await expect(page.getByRole('alert')).toContainText('Incorrect code');
  await page.getByLabel('Code').fill(totp(secret, 1));
  await page.getByRole('button', { name: 'Verify' }).click();
  await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
  await signOut(page);

  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.getByLabel('Code').fill(recovery.trim());
  await page.getByRole('button', { name: 'Verify' }).click();
  await expect(page.getByRole('heading', { name: 'Activities' })).toBeVisible();
});

test('set, edit and delete a goal', async ({ page }) => {
  const username = `e2e.goals${Date.now()}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const created = await page.request.post('/api/v1/users', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    data: {
      name: 'E2E Goals',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);
  await signOut(page);

  await signIn(page, username, password);
  await page.getByRole('link', { name: 'Goals', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Goals', level: 1 })).toBeVisible();
  await expect(page.getByText('No goals yet.')).toBeVisible();
  await expectAccessible(page);

  await page.getByLabel('Target').fill('10');
  await page.getByRole('button', { name: 'Save goal' }).click();
  await expect(page.getByText('Goal saved')).toBeVisible();
  const row = page.getByRole('row', { name: /Weekly · Run · Distance/ });
  await expect(row).toBeVisible();
  await expect(row.getByText('0 km / 10 km (0%)')).toBeVisible();
  await expectAccessible(page);

  await page.getByRole('button', { name: 'Edit goal Weekly · Run · Distance' }).click();
  await expect(page.getByRole('heading', { name: 'Edit goal' })).toBeVisible();
  await page.getByLabel('Target').fill('25');
  await page.getByRole('button', { name: 'Save goal' }).click();
  await expect(page.getByRole('row', { name: /0 km \/ 25 km/ })).toBeVisible();

  // A second goal for the same period, activity and measure is refused.
  await page.getByLabel('Target').fill('5');
  await page.getByRole('button', { name: 'Save goal' }).click();
  await expect(page.getByRole('alert')).toContainText('already have a goal');

  await page.getByRole('button', { name: 'Delete goal Weekly · Run · Distance' }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByText('No goals yet.')).toBeVisible();
});

test('share a public activity by link; an anonymous visitor sees only what the owner shares', async ({
  page,
  browser,
}) => {
  await signIn(page);
  const headers = { 'X-XSRF-TOKEN': await xsrf(page) };
  const upload = await page.request.post('/api/v1/activities/upload', {
    headers,
    multipart: { file: { name: 'share.gpx', mimeType: 'application/gpx+xml', buffer: gpx() } },
  });
  expect(upload.ok()).toBe(true);
  const [activity] = await upload.json();
  const title = `E2E shared run ${Date.now()}`;
  const update = await page.request.fetch(`/api/v1/activities/${activity.id}`, {
    method: 'PUT',
    headers,
    data: {
      name: title,
      description: 'Open to all',
      activity_type: activity.activity_type,
      visibility: 0,
    },
  });
  expect(update.ok()).toBe(true);

  const setLinks = async (on: boolean): Promise<void> => {
    await page.getByRole('link', { name: 'Server settings' }).click();
    const box = page.getByRole('checkbox', { name: 'Allow public links' });
    await (on ? box.check() : box.uncheck());
    await page.getByRole('button', { name: 'Save settings' }).click();
    await expect(page.getByText('Server settings saved')).toBeVisible();
  };

  const visitor = await browser.newContext();
  await visitor.route('**/tile.openstreetmap.org/**', (route) => route.abort());
  const anon = await visitor.newPage();
  try {
    // Links are off by default: the same address answers as if the activity did not exist.
    await anon.goto(`/share/${activity.id}`);
    await expect(anon.getByRole('heading', { name: 'This activity is not shared' })).toBeVisible();

    await setLinks(true);
    await page.goto(`/activities/${activity.id}`);
    await expect(page.getByRole('button', { name: 'Copy public link' })).toBeVisible();

    await anon.goto(`/share/${activity.id}`);
    await expect(anon.getByRole('heading', { name: title, level: 1 })).toBeVisible();
    await expect(anon.getByText('Open to all')).toBeVisible();
    await expect(anon.getByText('Shared by')).toHaveCount(0); // the owner's name needs its own switch
    await expect(anon.getByRole('region', { name: 'Route map' })).toBeVisible();
    await expectAccessible(anon);

    // Back to private: the link stops working.
    await page.request.fetch(`/api/v1/activities/${activity.id}`, {
      method: 'PUT',
      headers,
      data: {
        name: title,
        description: null,
        activity_type: activity.activity_type,
        visibility: 2,
      },
    });
    await anon.goto(`/share/${activity.id}`);
    await expect(anon.getByRole('heading', { name: 'This activity is not shared' })).toBeVisible();
  } finally {
    await setLinks(false);
    await visitor.close();
  }
});

test('find activities with the filters: name, type and sort', async ({ page }) => {
  const username = `e2e.filter${Date.now()}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const headers = { 'X-XSRF-TOKEN': await xsrf(page) };
  const created = await page.request.post('/api/v1/users', {
    headers,
    data: {
      name: 'E2E Filter',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);
  await signOut(page);

  await signIn(page, username, password);
  const userHeaders = { 'X-XSRF-TOKEN': await xsrf(page) };
  for (const [name, sport, daysAgo] of [
    ['Filter canal run', 'running', 3],
    ['Filter hill ride', 'cycling', 2],
    ['Filter river ride', 'cycling', 1],
  ] as const) {
    const upload = await page.request.post('/api/v1/activities/upload', {
      headers: userHeaders,
      multipart: {
        file: { name: 'f.gpx', mimeType: 'application/gpx+xml', buffer: gpx(name, sport, daysAgo) },
      },
    });
    expect(upload.ok()).toBe(true);
  }

  await page.goto('/activities');
  await expect(page.getByRole('link', { name: 'Filter canal run' })).toBeVisible();
  await expect(page.getByRole('link', { name: /^Filter / })).toHaveCount(3);
  await expectAccessible(page);

  await page.getByLabel('Search by name').fill('hill');
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByRole('link', { name: /^Filter / })).toHaveCount(1);
  await expect(page.getByRole('link', { name: 'Filter hill ride' })).toBeVisible();

  await page.getByRole('button', { name: 'Clear' }).click();
  await page.getByRole('combobox', { name: 'Activity type' }).click();
  await page.getByRole('option', { name: 'Ride', exact: true }).click();
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByRole('link', { name: /^Filter / })).toHaveCount(2);
  await expect(page.getByRole('link', { name: 'Filter canal run' })).toHaveCount(0);

  await page.getByRole('combobox', { name: 'Sort by' }).click();
  await page.getByRole('option', { name: 'Name' }).click();
  await page.getByRole('combobox', { name: 'Order' }).click();
  await page.getByRole('option', { name: 'Lowest or oldest first' }).click();
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByRole('link', { name: /^Filter / }).first()).toHaveText('Filter hill ride');
  await expectAccessible(page);
});

test('edit an activity: name, private notes and what to hide', async ({ page }) => {
  await signIn(page);
  const upload = await page.request.post('/api/v1/activities/upload', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    multipart: {
      file: {
        name: 'edit.gpx',
        mimeType: 'application/gpx+xml',
        buffer: gpx('E2E before edit', 'running', 4),
      },
    },
  });
  expect(upload.ok()).toBe(true);
  const [activity] = await upload.json();

  await page.goto(`/activities/${activity.id}`);
  await expect(page.getByRole('heading', { name: 'E2E before edit', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Edit details' }).click();
  await expect(page.getByRole('checkbox', { name: 'Use my privacy settings' })).toBeChecked();
  await expectAccessible(page);

  const renamed = `E2E after edit ${Date.now()}`;
  await page.getByLabel('Name', { exact: true }).fill(renamed);
  await page.getByLabel('Private notes (only you can read these)').fill('Felt strong on the hill');
  await page.getByLabel('How it felt (0–100)').fill('80');
  await page.getByRole('checkbox', { name: 'Use my privacy settings' }).uncheck();
  await page.getByRole('checkbox', { name: 'Heart rate' }).check();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Activity saved')).toBeVisible();
  await expect(page.getByRole('heading', { name: renamed, level: 1 })).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'Edit details' }).click();
  await expect(page.getByLabel('Private notes (only you can read these)')).toHaveValue(
    'Felt strong on the hill',
  );
  await expect(page.getByRole('checkbox', { name: 'Use my privacy settings' })).not.toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Heart rate' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Map' })).not.toBeChecked();
  await expectAccessible(page);
});

test('a save from a stale tab is refused instead of overwriting the newer edit', async ({
  page,
}) => {
  await signIn(page);
  const upload = await page.request.post('/api/v1/activities/upload', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    multipart: {
      file: {
        name: 'tabs.gpx',
        mimeType: 'application/gpx+xml',
        buffer: gpx('E2E two tabs', 'running', 5),
      },
    },
  });
  const [activity] = await upload.json();

  const second = await page.context().newPage();
  await page.goto(`/activities/${activity.id}`);
  await second.goto(`/activities/${activity.id}`);
  for (const tab of [page, second]) {
    await expect(tab.getByRole('heading', { name: 'E2E two tabs', level: 1 })).toBeVisible();
    await tab.getByRole('button', { name: 'Edit details' }).click();
  }

  await page.getByLabel('Name', { exact: true }).fill('First tab wins');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Activity saved')).toBeVisible();

  await second.getByLabel('Name', { exact: true }).fill('Second tab loses');
  await second.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(second.getByRole('alert')).toContainText('changed somewhere else');
  await second.reload();
  await expect(second.getByRole('heading', { name: 'First tab wins', level: 1 })).toBeVisible();
  await second.close();
});

test('heart rate zones on an activity and today’s numbers on the home page', async ({ page }) => {
  await signIn(page);
  const headers = { 'X-XSRF-TOKEN': await xsrf(page) };
  const me = await (await page.request.get('/api/v1/users/me')).json();
  expect(
    (
      await page.request.put('/api/v1/users/me', { headers, data: { ...me, max_heart_rate: 180 } })
    ).ok(),
  ).toBe(true);
  const upload = await page.request.post('/api/v1/activities/upload', {
    headers,
    multipart: {
      file: {
        name: 'zones.gpx',
        mimeType: 'application/gpx+xml',
        buffer: gpx('E2E zones', 'running', 6),
      },
    },
  });
  const [activity] = await upload.json();

  await page.goto(`/activities/${activity.id}`);
  await expect(page.getByRole('heading', { name: 'Heart rate zones' })).toBeVisible();
  await expect(page.getByRole('progressbar', { name: /Zone 1 \(< 108 bpm\)/ })).toBeVisible();
  await expectAccessible(page);

  const today = new Date().toISOString().slice(0, 10);
  expect(
    (
      await page.request.post('/api/v1/health/steps', {
        headers,
        data: { date: today, steps: 4321 },
      })
    ).ok(),
  ).toBe(true);
  await page.goto('/activities');
  const section = page.getByRole('region', { name: 'Today' });
  await expect(section.getByRole('link', { name: 'Steps' })).toBeVisible();
  await expect(section).toContainText('4,321');
  await expectAccessible(page);
});

test('make an API key, upload with it, then revoke and delete it', async ({ page, request }) => {
  const username = `e2e.keys${Date.now()}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const created = await page.request.post('/api/v1/users', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    data: {
      name: 'E2E Keys',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);
  await signOut(page);

  await signIn(page, username, password);
  await page.goto('/security');
  await expect(page.getByRole('heading', { name: 'API keys' })).toBeVisible();
  await expect(page.getByText('No API keys yet.')).toBeVisible();
  await expectAccessible(page);

  await page.getByLabel('Name', { exact: true }).fill('Watch sync');
  await page.getByLabel('Current password').last().fill(password);
  await page.getByRole('button', { name: 'Make key' }).click();
  const key = (await page.locator('code.secret').innerText()).trim();
  expect(key).toMatch(/^gravelio_/);
  await expect(page.getByRole('row', { name: /Watch sync/ })).toContainText('Active');
  await expectAccessible(page);
  await page.getByRole('button', { name: 'I have copied it' }).click();

  // A program with only the key can upload, with no cookie and no antiforgery token.
  const upload = (days: number) =>
    request.post('/api/v1/activities/upload', {
      headers: { 'X-API-Key': key },
      multipart: {
        file: {
          name: 'sync.gpx',
          mimeType: 'application/gpx+xml',
          buffer: gpx('E2E key upload', 'running', days),
        },
      },
    });
  expect((await upload(5)).status()).toBe(200);

  await page.getByRole('button', { name: 'Revoke key Watch sync' }).click();
  await expect(page.getByRole('row', { name: /Watch sync/ })).toContainText('Revoked');
  expect((await upload(6)).status()).toBe(401);

  await page.getByRole('button', { name: 'Delete key Watch sync' }).click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(page.getByText('No API keys yet.')).toBeVisible();
});

test('a second recording of the same workout is kept hidden and reported', async ({ page }) => {
  const username = `e2e.dup${Date.now()}`;
  const password = 'Str0ng-pass!';

  await signIn(page);
  const created = await page.request.post('/api/v1/users', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    data: {
      name: 'E2E Dup',
      username,
      email: `${username}@example.com`,
      access_type: 'regular',
      active: true,
      password,
    },
  });
  expect(created.status()).toBe(201);
  await signOut(page);

  await signIn(page, username, password);
  const headers = { 'X-XSRF-TOKEN': await xsrf(page) };
  const start = Date.now() - 7 * 86_400_000;
  for (const name of ['E2E watch recording', 'E2E phone recording']) {
    const upload = await page.request.post('/api/v1/activities/upload', {
      headers,
      multipart: {
        file: {
          name: 'w.gpx',
          mimeType: 'application/gpx+xml',
          buffer: gpx(name, 'running', 7, start),
        },
      },
    });
    expect(upload.ok()).toBe(true);
  }

  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: 'Notifications', level: 1 })).toBeVisible();
  const note = page.getByRole('link', { name: /E2E phone recording.*duplicate/ });
  await expect(note).toBeVisible();
  await expectAccessible(page);
  await note.click();
  await expect(page.getByRole('heading', { name: 'E2E phone recording', level: 1 })).toBeVisible();
  await expect(page.getByText('This activity is hidden.')).toBeVisible();

  await page.getByRole('button', { name: 'Edit details' }).click();
  await page.getByRole('checkbox', { name: /Hide this activity from everyone/ }).uncheck();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Activity saved')).toBeVisible();
  await expect(page.getByText('This activity is hidden.')).toHaveCount(0);
  await expectAccessible(page);
});

test('export my data and import it into another account', async ({ page }) => {
  const stamp = Date.now();
  const password = 'Str0ng-pass!';
  const names = [`e2e.xfer.a${stamp}`, `e2e.xfer.b${stamp}`];

  await signIn(page);
  const adminHeaders = { 'X-XSRF-TOKEN': await xsrf(page) };
  for (const username of names) {
    const created = await page.request.post('/api/v1/users', {
      headers: adminHeaders,
      data: {
        name: 'E2E Xfer',
        username,
        email: `${username}@example.com`,
        access_type: 'regular',
        active: true,
        password,
      },
    });
    expect(created.status()).toBe(201);
  }
  await signOut(page);

  await signIn(page, names[0], password);
  const upload = await page.request.post('/api/v1/activities/upload', {
    headers: { 'X-XSRF-TOKEN': await xsrf(page) },
    multipart: {
      file: {
        name: 'x.gpx',
        mimeType: 'application/gpx+xml',
        buffer: gpx('E2E exported run', 'running', 9),
      },
    },
  });
  expect(upload.ok()).toBe(true);

  await page.goto('/profile');
  await expect(page.getByRole('heading', { name: 'Your data' })).toBeVisible();
  await expectAccessible(page);
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('link', { name: 'Export my data' }).click(),
  ]);
  expect(download.suggestedFilename()).toMatch(/^gravelio-export-\d{8}\.zip$/);
  const saved = test.info().outputPath('export.zip'); // the import only takes a .zip, and Playwright's own copy has no extension
  await download.saveAs(saved);
  await signOut(page);

  await signIn(page, names[1], password);
  await page.goto('/profile');
  await page.locator('input[type=file]').setInputFiles(saved);
  await expect(page.getByText(/Imported 1 activities/)).toBeVisible();
  await page.goto('/activities');
  await expect(page.getByRole('link', { name: 'E2E exported run' })).toBeVisible();
});

test('sign out ends the session', async ({ page }) => {
  await signIn(page);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/activities');
  await expect(page).toHaveURL(/\/login$/);
});
