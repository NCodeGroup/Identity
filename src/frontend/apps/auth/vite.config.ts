import { defineBackendServedSurfaceConfig } from '@ncode/vite-config';

// The interactive login/consent surface. The OpenID Provider host serves this bundle beneath each tenant's route and sets
// the document's base to it, so the bundle is built with a relative base (ADR-0193).
export default defineBackendServedSurfaceConfig({
  coverage: { statements: 100, branches: 100, functions: 100, lines: 100 },
});
