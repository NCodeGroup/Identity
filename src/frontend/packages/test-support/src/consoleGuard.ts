import { formatConsoleArgs } from './formatConsoleArgs.js';

/**
 * The console methods whose output means a test hit a defect rather than produced information: a React
 * "not wrapped in act(...)" warning, a missing-key or invalid-prop warning, a jsdom "Not implemented" error, or a
 * failure that production code logged instead of surfacing.
 */
const guardedMethods = ['error', 'warn'] as const;

type GuardedMethod = (typeof guardedMethods)[number];

/**
 * Output that depends on how loaded the machine is rather than on the code under test: failing on it would fail tests
 * at random, and printing it would only bury real output, so the guard drops it. Keep this list to timing diagnostics:
 * Redux Toolkit's development-mode middleware warns when its immutability or serializability check runs longer than
 * 32 ms, which v8 coverage routinely causes on store-backed tests. The checks' real findings — a mutated or
 * non-serializable value — are reported through `console.error` and still fail the test.
 */
const timingDependentWarnings: readonly RegExp[] = [
  /^(?:Immutable|Serializable)StateInvariantMiddleware took \d+ms, which is more than the warning threshold of \d+ms\./,
];

/** The slice of `console` the guard intercepts. */
export type GuardedConsole = Pick<Console, GuardedMethod>;

/** Records what a test logs to the guarded console methods. */
export type ConsoleGuard = {
  /** Starts a fresh recording by routing the guarded methods through recorders that still forward to the originals. */
  readonly start: () => void;
  /** Restores the original methods and returns every message recorded since {@link ConsoleGuard.start}. */
  readonly stop: () => readonly string[];
};

/** The Vitest lifecycle hooks {@link failOnConsole} registers on, injected so this package never loads `vitest`. */
export type TestHooks = {
  readonly beforeEach: (fn: () => void) => unknown;
  readonly afterEach: (fn: () => void) => unknown;
};

/**
 * Creates a guard over the given console. The recorders are created once and always append to the current recording,
 * so a test that spies on a guarded method and is later restored by `vi.restoreAllMocks()` still records into the
 * active test rather than a stale one.
 */
export function createConsoleGuard(target: GuardedConsole = console): ConsoleGuard {
  const originals: Record<GuardedMethod, (...args: unknown[]) => void> = {
    error: target.error,
    warn: target.warn,
  };
  let recorded: string[] = [];
  const recorders = Object.fromEntries(
    guardedMethods.map((method) => [
      method,
      (...args: unknown[]) => {
        const message = formatConsoleArgs(args);
        if (method === 'warn' && timingDependentWarnings.some((pattern) => pattern.test(message))) {
          return;
        }
        recorded.push(`console.${method}: ${message}`);
        originals[method].apply(target, args);
      },
    ]),
  ) as Record<GuardedMethod, (...args: unknown[]) => void>;

  return {
    start: () => {
      recorded = [];
      for (const method of guardedMethods) {
        target[method] = recorders[method];
      }
    },
    stop: () => {
      for (const method of guardedMethods) {
        target[method] = originals[method];
      }
      const messages = recorded;
      recorded = [];
      return messages;
    },
  };
}

/**
 * Fails every test that logs to `console.error` or `console.warn` — the test-suite equivalent of warnings-as-errors.
 * Call it once from a Vitest setup file. A test that deliberately exercises a logging path opts out locally by
 * replacing the method with `vi.spyOn(console, 'error').mockImplementation(...)` and asserting on the spy.
 */
export function failOnConsole(hooks: TestHooks, target: GuardedConsole = console): void {
  const guard = createConsoleGuard(target);
  hooks.beforeEach(() => guard.start());
  hooks.afterEach(() => {
    const messages = guard.stop();
    if (messages.length > 0) {
      throw new Error(
        `The test logged ${messages.length} console warning(s) or error(s). Fix the cause — for a React act(...) ` +
          'warning, await the settled state with findBy…/waitFor — or, when the output is the behavior under test, ' +
          `mock the console method and assert on it.\n\n${messages.join('\n\n')}`,
      );
    }
  });
}
