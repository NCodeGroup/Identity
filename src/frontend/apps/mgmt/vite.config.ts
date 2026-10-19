import { defineStandaloneSurfaceConfig } from '@ncode/vite-config';

// The Management console. Port 5173 is the one its seeded redirect URI, the developer CORS allowlist, and the AppHost
// endpoint assume; the bootstrap document comes from the Playground OP host (`api`).
export default defineStandaloneSurfaceConfig({
  port: 5173,
  bootstrapHost: { service: 'api', fallbackOrigin: 'https://localhost:5001' },
  coverage: { statements: 97, branches: 95, functions: 100, lines: 97 },
});
