// SPDX-License-Identifier: LGPL-3.0-or-later
import { defineConfig } from '@playwright/test'

const port = 5080

// End to end means the real server, with its real packages, on a real PostgreSQL (OSME_DB).
export default defineConfig({
  testDir: './e2e',
  use: {
    baseURL: `http://localhost:${port}`,
    // Set PLAYWRIGHT_CHROMIUM to use a browser already installed instead of Playwright's download.
    launchOptions: { executablePath: process.env.PLAYWRIGHT_CHROMIUM || undefined },
  },
  webServer: {
    command: 'dotnet run --project ../Host',
    url: `http://localhost:${port}/api/packages`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
