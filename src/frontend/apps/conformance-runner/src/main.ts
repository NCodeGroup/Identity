/**
 * Node entry point for the headless conformance probe runner. Parses CLI arguments and environment variables, runs
 * the probe-runner suite against the configured provider, prints the report, and exits non-zero when any probe fails
 * so the runner is a usable CI pre-flight for the external certified suite (Phase 4 / `T1`). All logic lives in the
 * UI-free, unit-tested modules; this file only wires the process to them.
 */

import type { ProbeFetch } from '@ncode/conformance-core';
import { parseArgs, USAGE } from './config.js';
import { formatReport, exitCode, toJson } from './report.js';
import { runConformanceRunner } from './runner.js';

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
    const report = await runConformanceRunner(config, { fetchFn });
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
