/**
 * Node entry point for the headless conformance probe runner. Binds the real process — argv, environment, filesystem,
 * network, stdout/stderr, and exit code — to {@link runCli}, which parses the arguments, runs the probe-runner suite,
 * prints the report, and returns a non-zero exit code when any probe fails, so the runner is a usable CI pre-flight for
 * the external certified suite (Phase 4 / #124). All logic lives in the UI-free, unit-tested modules; this file only
 * wires the process to them.
 */

import { readFile } from 'node:fs/promises';
import { runCli } from './cli.js';

runCli(process.argv.slice(2), process.env, {
  readFile: (path) => readFile(path, 'utf8'),
  fetch: (url, init) => fetch(url, init as RequestInit),
  log: (message) => console.log(message),
  error: (message) => console.error(message),
})
  .then((code) => {
    process.exitCode = code;
  })
  .catch(() => {
    process.exitCode = 2;
  });
