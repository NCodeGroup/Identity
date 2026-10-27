/**
 * `@ncode/conformance-core` — framework-free OAuth 2.x / OpenID Connect conformance core.
 *
 * Holds the UI-free, injectable-fetch logic shared by every conformance surface: the discovery fetch, the
 * discovery-metadata security audit, the full published spec-coverage catalog, and the browser-drivable active probes.
 * The browser dashboard ([apps/conformance](../../../apps/conformance)) renders this; the headless probe runner
 * ([apps/conformance-runner](../../../apps/conformance-runner)) reuses the discovery fetch, the `RawDiscovery` shape,
 * the coverage catalog, and the probe result types to exercise the confidential and server-initiated legs a browser
 * cannot drive. See [ADR-0084](../../../../../docs/decisions/0084-spec-coverage-conformance-harness.md).
 */

export {
  auditDiscovery,
  summarize,
  type AuditReport,
  type Finding,
  type ProfileReadiness,
  type RawDiscovery,
  type Severity,
} from './audit.js';

export {
  buildCoverageReport,
  type CoverageCategory,
  type CoverageChannel,
  type CoverageEntry,
  type CoverageReport,
  type CoverageStatus,
} from './coverage.js';

export { fetchRawDiscovery, type FetchLike } from './discovery.js';

export {
  probeJwks,
  probePar,
  probeTokenErrorShape,
  probeUserInfoUnauthorized,
  runProbes,
  type ProbeFetch,
  type ProbeOutcome,
  type ProbeReport,
  type ProbeResponse,
  type ProbeResult,
  type ProbeRunOptions,
} from './probes.js';
