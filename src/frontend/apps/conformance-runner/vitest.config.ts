import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    setupFiles: ['./vitest.setup.ts'],
    // A test that makes no assertion fails (ADR-0151).
    expect: { requireAssertions: true },
    include: ['src/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.ts'],
      exclude: ['src/**/*.test.ts', 'src/main.ts'],
      // Floors at the level reached, rounded down (ADR-0151): raise them as coverage rises; lowering one needs a reason.
      thresholds: { statements: 100, branches: 100, functions: 100, lines: 100 },
    },
  },
});
