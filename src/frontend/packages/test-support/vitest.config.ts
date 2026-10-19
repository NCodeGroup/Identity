import { defineConfig } from 'vitest/config';

// The one workspace that cannot use `@ncode/vite-config`: the preset depends on this package for its own console guard,
// and turbo rejects a cyclic workspace graph. Keep these gates in step with the preset's `testGates` (ADR-0157).
export default defineConfig({
  test: {
    environment: 'node',
    setupFiles: ['./vitest.setup.ts'],
    // A test that makes no assertion fails (ADR-0151).
    expect: { requireAssertions: true },
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/**/*.stories.{ts,tsx}',
        'src/main.{ts,tsx}',
        'src/index.ts',
      ],
      // Floors at the level reached, rounded down (ADR-0151): raise them as coverage rises; lowering one needs a reason.
      thresholds: { statements: 100, branches: 100, functions: 100, lines: 100 },
    },
  },
});
