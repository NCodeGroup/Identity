import { defineStandaloneSurfaceConfig } from '@ncode/vite-config';

// The flow explorer. Port 5174 is the one the seeded `playground-spa` redirect URI, the developer CORS allowlist, and
// the AppHost endpoint assume; the bootstrap document comes from the Playground OP host (`api`).
export default defineStandaloneSurfaceConfig({
  port: 5174,
  bootstrapHost: { service: 'api', fallbackOrigin: 'https://localhost:5001' },
  coverage: { statements: 98, branches: 96, functions: 100, lines: 98 },
});
