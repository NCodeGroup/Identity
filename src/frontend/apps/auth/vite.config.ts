/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The OpenID Provider host serves this bundle under /account/*, so assets must resolve from that base.
export default defineConfig({
  base: '/account/',
  plugins: [react()],
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    // jsdom + MUI renders run for seconds under v8 coverage on shared CI runners; see "jsdom test timeout" in the
    // frontend README.
    testTimeout: 15_000,
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    // A test that makes no assertion fails (ADR-0151).
    expect: { requireAssertions: true },
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/main.tsx'],
      // Floors at the level reached, rounded down (ADR-0151): raise them as coverage rises; lowering one needs a reason.
      thresholds: { statements: 100, branches: 100, functions: 100, lines: 100 },
    },
  },
});
