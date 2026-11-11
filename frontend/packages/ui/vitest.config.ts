import { defineTestConfig } from '@ncode/vite-config';

export default defineTestConfig({
  environment: 'jsdom',
  coverage: { statements: 97, branches: 97, functions: 100, lines: 96 },
});
