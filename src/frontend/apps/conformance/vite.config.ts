import { defineStandaloneSurfaceConfig } from '@ncode/vite-config';

// The conformance posture dashboard. Port 5175 is the one the developer CORS allowlist and the AppHost endpoint assume.
// Its bootstrap document comes from the dedicated conformance OP host (`conformanceapi`): a second Playground instance
// launched under the Conformance environment that advertises the available-but-off-by-default capabilities (Session
// Management, Front-Channel Logout, client_credentials — ADR-0088), so the dashboard measures those as implemented
// while the default `api` stays locked down.
export default defineStandaloneSurfaceConfig({
  port: 5175,
  bootstrapHost: { service: 'conformanceapi', fallbackOrigin: 'https://localhost:5003' },
  coverage: { statements: 100, branches: 100, functions: 100, lines: 100 },
});
