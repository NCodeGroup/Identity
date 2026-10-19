import { defineTestConfig } from '@ncode/vite-config';

export default defineTestConfig({
  environment: 'jsdom',
  coverage: { statements: 100, branches: 100, functions: 100, lines: 100 },
});
