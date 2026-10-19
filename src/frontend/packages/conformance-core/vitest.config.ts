import { defineTestConfig } from '@ncode/vite-config';

export default defineTestConfig({
  environment: 'node',
  coverage: { statements: 100, branches: 98, functions: 100, lines: 100 },
});
