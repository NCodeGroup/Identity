/// <reference types="vitest" />
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    // jsdom + MUI renders run for seconds under v8 coverage on shared CI runners; see "jsdom test timeout" in the
    // frontend README.
    testTimeout: 15_000,
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.test.tsx'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.tsx'],
      exclude: ['src/**/*.test.tsx', 'src/**/*.stories.tsx'],
    },
  },
});
