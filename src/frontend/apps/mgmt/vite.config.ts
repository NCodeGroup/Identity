import { defineBffServedSurfaceConfig } from '@ncode/vite-config';

// The Management console. Its BFF host (`NCode.Identity.ManagementConsole.Host`) serves the bundle at the root of its
// origin under each response's CSP nonce; there is no dev server, so `dev` rebuilds `dist`.
export default defineBffServedSurfaceConfig({
  coverage: { statements: 97, branches: 95, functions: 100, lines: 97 },
});
