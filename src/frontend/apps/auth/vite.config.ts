import { defineBackendServedSurfaceConfig } from '@ncode/vite-config';

// The interactive login/consent surface. The OpenID Provider host serves this bundle under /account/*, so assets must
// resolve from that base.
export default defineBackendServedSurfaceConfig({
  base: '/account/',
  coverage: { statements: 100, branches: 100, functions: 100, lines: 100 },
});
