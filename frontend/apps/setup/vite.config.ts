import { defineBackendServedSurfaceConfig } from '@ncode/vite-config';

// The first-run setup wizard. The OpenID Provider host serves this bundle at /setup and sets the document's base to it,
// so the bundle is built with a relative base and reaches its API through relative URLs (ADR-1193).
export default defineBackendServedSurfaceConfig({
  coverage: { statements: 100, branches: 100, functions: 100, lines: 100 },
});
