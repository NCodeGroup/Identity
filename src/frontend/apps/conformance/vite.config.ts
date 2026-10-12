/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Origin of the Playground OP host (`api`) that publishes the runtime bootstrap document (ADR-0064, backlog U3).
// Under the Aspire local-dev orchestrator (ADR-0071) the `conformance` resource is `WithReference(api)`, so Aspire
// injects this service-discovery variable; fall back to the pinned dev origin when running the dev server outside Aspire.
const apiOrigin =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'https://localhost:5001';

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
