/**
 * CLI orchestration (UI-free, unit-tested) — turns the command line and environment into a probe run: parses the
 * arguments, loads the configured signing keys, runs the probe-runner suite, prints the report, and maps the outcome
 * to a process exit code. Every process side effect (file reads, network, stdout/stderr) is injected through
 * {@link CliIo} so the whole CLI is testable without a process, filesystem, or network; `main.ts` only binds the real
 * process to {@link runCli}.
 */

import type { ProbeFetch } from '@ncode/conformance-core';
import { parseArgs, USAGE, type RunnerConfig } from './config.js';
import { exitCode, formatReport, toJson } from './report.js';
import { runConformanceRunner, type RunnerDeps } from './runner.js';
import { createClientAssertionSigner, createDpopProofFactory, loadSigningKey } from './signer.js';

/** The process side effects the CLI performs, injected so it stays testable. */
export type CliIo = {
  /** Reads a UTF-8 text file (the PEM/JWK signing keys). */
  readonly readFile: (path: string) => Promise<string>;
  /** Performs the HTTP requests against the provider. */
  readonly fetch: ProbeFetch;
  /** Writes a message to standard output. */
  readonly log: (message: string) => void;
  /** Writes a message to standard error. */
  readonly error: (message: string) => void;
};

/** The exit code for invalid arguments or a run that could not complete (distinct from `1`, a failed probe). */
const EXIT_ERROR = 2;

/** Loads the configured PEM/JWK signing keys and assembles the runner dependencies (fetch + asymmetric signers). */
async function buildDeps(config: RunnerConfig, io: CliIo): Promise<RunnerDeps> {
  let signClientAssertion: RunnerDeps['signClientAssertion'];
  let createDpopProof: RunnerDeps['createDpopProof'];
  if (config.privateKeyPath !== undefined) {
    const material = await io.readFile(config.privateKeyPath);
    const key = loadSigningKey(material, config.privateKeyAlg ? { alg: config.privateKeyAlg } : {});
    signClientAssertion = createClientAssertionSigner(key);
  }
  if (config.dpopKeyPath !== undefined) {
    const material = await io.readFile(config.dpopKeyPath);
    const key = loadSigningKey(material, config.dpopKeyAlg ? { alg: config.dpopKeyAlg } : {});
    createDpopProof = createDpopProofFactory(key);
  }
  return {
    fetchFn: io.fetch,
    ...(signClientAssertion ? { signClientAssertion } : {}),
    ...(createDpopProof ? { createDpopProof } : {}),
  };
}

/**
 * Runs the CLI for the given arguments (after the node/script prefix) and environment, returning the process exit
 * code: `0` when no probe fails (or for `--help`), `1` when any probe fails, and `2` for invalid arguments or a run
 * that could not complete (for example, unreachable discovery metadata or an unloadable key).
 */
export async function runCli(
  argv: readonly string[],
  env: Readonly<Record<string, string | undefined>>,
  io: CliIo,
): Promise<number> {
  const parsed = parseArgs(argv, env);
  if (parsed.kind === 'help') {
    io.log(USAGE);
    return 0;
  }
  if (parsed.kind === 'error') {
    io.error(parsed.errors.join('\n'));
    io.error(`\n${USAGE}`);
    return EXIT_ERROR;
  }

  const { config } = parsed;
  try {
    const deps = await buildDeps(config, io);
    const report = await runConformanceRunner(config, deps);
    io.log(config.json ? toJson(report, config.issuer) : formatReport(report, config.issuer));
    return exitCode(report);
  } catch (cause) {
    io.error(`Runner failed: ${cause instanceof Error ? cause.message : String(cause)}`);
    return EXIT_ERROR;
  }
}
