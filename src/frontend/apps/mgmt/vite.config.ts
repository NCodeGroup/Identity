/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Origin of the Playground OP host (`api`) that publishes the runtime bootstrap document (ADR-0064, backlog U4).
// Under the Aspire local-dev orchestrator (ADR-0071) the `mgmt` resource is `WithReference(api)`, so Aspire injects
// this service-discovery variable; fall back to the pinned dev origin when running the dev server outside Aspire.
const apiOrigin =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'https://localhost:5001';

export default defineConfig({
  plugins: [react()],
  // The console is served standalone by Vite, so Vite is the "serving host" that must publish the bootstrap document
  // the bundle fetches at boot. Rather than ship a drift-prone static copy, proxy the well-known path to the OP host's
  // runtime endpoint so local dev exercises the real document (ADR-0064) — one source of truth across every tier.
  server: {
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
