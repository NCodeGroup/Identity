/**
 * Node entry point for the headless conformance probe runner. Parses CLI arguments and environment variables, runs
 * the probe-runner suite against the configured provider, prints the report, and exits non-zero when any probe fails
 * so the runner is a usable CI pre-flight for the external certified suite (Phase 4 / `T1`). All logic lives in the
 * UI-free, unit-tested modules; this file only wires the process to them.
 */

import type { ProbeFetch } from '@ncode/conformance-core';
import { readFile } from 'node:fs/promises';
import { parseArgs, USAGE, type RunnerConfig } from './config.js';
import { formatReport, exitCode, toJson } from './report.js';
import { runConformanceRunner, type RunnerDeps } from './runner.js';
import { createClientAssertionSigner, createDpopProofFactory, loadSigningKey } from './signer.js';

/** Loads the configured PEM/JWK signing keys and assembles the runner dependencies (fetch + asymmetric signers). */
async function buildDeps(config: RunnerConfig, fetchFn: ProbeFetch): Promise<RunnerDeps> {
  let signClientAssertion: RunnerDeps['signClientAssertion'];
  let createDpopProof: RunnerDeps['createDpopProof'];
  if (config.privateKeyPath !== undefined) {
    const material = await readFile(config.privateKeyPath, 'utf8');
    const key = loadSigningKey(material, config.privateKeyAlg ? { alg: config.privateKeyAlg } : {});
    signClientAssertion = createClientAssertionSigner(key);
  }
  if (config.dpopKeyPath !== undefined) {
    const material = await readFile(config.dpopKeyPath, 'utf8');
    const key = loadSigningKey(material, config.dpopKeyAlg ? { alg: config.dpopKeyAlg } : {});
    createDpopProof = createDpopProofFactory(key);
  }
  return {
    fetchFn,
    ...(signClientAssertion ? { signClientAssertion } : {}),
    ...(createDpopProof ? { createDpopProof } : {}),
  };
}

async function main(): Promise<number> {
  const parsed = parseArgs(process.argv.slice(2), process.env);
  if (parsed.kind === 'help') {
    console.log(USAGE);
    return 0;
  }
  if (parsed.kind === 'error') {
    console.error(parsed.errors.join('\n'));
    console.error(`\n${USAGE}`);
    return 2;
  }

  const { config } = parsed;
  const fetchFn: ProbeFetch = (url, init) => fetch(url, init as RequestInit);
  try {
    const deps = await buildDeps(config, fetchFn);
    const report = await runConformanceRunner(config, deps);
    console.log(config.json ? toJson(report, config.issuer) : formatReport(report, config.issuer));
    return exitCode(report);
  } catch (cause) {
    console.error(`Runner failed: ${cause instanceof Error ? cause.message : String(cause)}`);
    return 2;
  }
}

main()
  .then((code) => {
    process.exitCode = code;
  })
  .catch(() => {
    process.exitCode = 2;
  });
