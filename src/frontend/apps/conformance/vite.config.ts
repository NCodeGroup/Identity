/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Origin of the dedicated conformance OP host (`conformanceapi`) that publishes the runtime bootstrap document
// (ADR-0064, backlog U3). It is a SECOND Playground instance launched under the Conformance environment that advertises
// the available-but-off-by-default capabilities (Session Management, Front-Channel Logout, client_credentials — ADR-0088),
// so the dashboard measures those as implemented while the default `api` stays locked down. Under the Aspire local-dev
// orchestrator (ADR-0071) the `conformance` resource is `WithReference(conformanceapi)`, so Aspire injects this
// service-discovery variable; fall back to the pinned conformance dev origin when running the dev server outside Aspire.
const apiOrigin =
  process.env['services__conformanceapi__https__0'] ??
  process.env['services__conformanceapi__http__0'] ??
  'https://localhost:5003';

export default defineConfig({
  base: '/',
  plugins: [react()],
  // This dashboard is served standalone by Vite on its own pinned port (5175 — the port the developer CORS allowlist
  // assumes). Vite is the serving host and must publish the bootstrap document the bundle fetches at boot; proxy the
  // well-known path to the OP host so local dev reads the real runtime document (ADR-0064) rather than a static copy.
  server: {
    port: 5175,
    strictPort: true,
    proxy: {
      '/config/bootstrap.json': {
        target: apiOrigin,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    // jsdom + MUI renders run for seconds under v8 coverage on shared CI runners; see "jsdom test timeout" in the
    // frontend README.
    testTimeout: 15_000,
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/main.tsx'],
    },
  },
});
