// SPDX-License-Identifier: LGPL-3.0-or-later
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

const server = 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  // The server serves the compiled interface from its wwwroot.
  build: { outDir: '../Host/wwwroot', emptyOutDir: true },
  server: { proxy: { '/api': server } },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test-setup.ts',
    include: ['src/**/*.test.tsx'],
    restoreMocks: true,
  },
})
