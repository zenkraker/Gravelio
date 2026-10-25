import { defineConfig } from '@playwright/test';

// Needs the API running against PostgreSQL (see CLAUDE.md); `ng serve` proxies /api to it.
export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    launchOptions: { executablePath: process.env['PLAYWRIGHT_CHROMIUM_PATH'] || undefined },
  },
  webServer: process.env['E2E_BASE_URL']
    ? undefined
    : {
        command: 'pnpm start',
        url: 'http://localhost:4200',
        reuseExistingServer: true,
        timeout: 120_000,
      },
});
