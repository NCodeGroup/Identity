/**
 * Runner orchestration (UI-free, unit-tested). Fetches the provider's discovery metadata, then runs every
 * probe-runner probe against it under a single run-wide timeout. Discovery fetch and the probe `fetch` are injected
 * so the orchestration is testable without a network.
 */

import { fetchRawDiscovery, type ProbeFetch, type ProbeReport } from '@ncode/conformance-core';
import { toProbeContext, type RunnerConfig } from './config.js';
import { runProbeRunner } from './probeRunner.js';
import type { ClientAssertionSigner, DpopProofFactory } from './probeRunner.js';

/** Injected dependencies: the fetch implementation and the optional asymmetric signers. */
export type RunnerDeps = {
  readonly fetchFn: ProbeFetch;
  /** Signs the `private_key_jwt` client assertion; absent when no client key is configured. */
  readonly signClientAssertion?: ClientAssertionSigner;
  /** Builds DPoP proofs; absent when no DPoP key is configured. */
  readonly createDpopProof?: DpopProofFactory;
};

/** Loads the provider's discovery metadata and runs the probe-runner suite, aggregating the outcomes. */
export async function runConformanceRunner(
  config: RunnerConfig,
  deps: RunnerDeps,
): Promise<ProbeReport> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), config.timeoutMs);
  try {
    const raw = await fetchRawDiscovery(config.issuer, deps.fetchFn, controller.signal);
    return await runProbeRunner(raw, deps.fetchFn, {
      ...toProbeContext(config),
      ...(deps.signClientAssertion ? { signClientAssertion: deps.signClientAssertion } : {}),
      ...(deps.createDpopProof ? { createDpopProof: deps.createDpopProof } : {}),
      signal: controller.signal,
    });
  } finally {
    clearTimeout(timer);
  }
}
