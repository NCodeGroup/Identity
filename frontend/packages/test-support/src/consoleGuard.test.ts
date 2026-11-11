import { describe, expect, it, vi } from 'vitest';
import { createConsoleGuard, failOnConsole, type GuardedConsole } from './consoleGuard.js';

function fakeConsole() {
  const error = vi.fn();
  const warn = vi.fn();
  return { target: { error, warn } as GuardedConsole, error, warn };
}

function captureHooks() {
  const hooks: { before?: () => void; after?: () => void } = {};
  return {
    hooks,
    registrar: {
      beforeEach: (fn: () => void) => {
        hooks.before = fn;
      },
      afterEach: (fn: () => void) => {
        hooks.after = fn;
      },
    },
  };
}

describe('createConsoleGuard', () => {
  it('records formatted error and warn output and forwards it to the original methods', () => {
    const { target, error, warn } = fakeConsole();
    const guard = createConsoleGuard(target);

    guard.start();
    target.error('An update to %s was not wrapped in act(...).', 'Dialog');
    target.warn('deprecated', 42);
    const messages = guard.stop();

    expect(messages).toEqual([
      'console.error: An update to Dialog was not wrapped in act(...).',
      'console.warn: deprecated 42',
    ]);
    expect(error).toHaveBeenCalledWith('An update to %s was not wrapped in act(...).', 'Dialog');
    expect(warn).toHaveBeenCalledWith('deprecated', 42);
  });

  it('restores the original methods on stop and records nothing afterwards', () => {
    const { target, error, warn } = fakeConsole();
    const guard = createConsoleGuard(target);

    guard.start();
    expect(target.error).not.toBe(error);
    expect(guard.stop()).toEqual([]);

    expect(target.error).toBe(error);
    expect(target.warn).toBe(warn);
    target.error('after stop');
    expect(guard.stop()).toEqual([]);
  });

  it('starts every recording empty', () => {
    const { target } = fakeConsole();
    const guard = createConsoleGuard(target);

    guard.start();
    target.warn('first test');
    guard.start();
    target.warn('second test');

    expect(guard.stop()).toEqual(['console.warn: second test']);
  });

  it('records a stale recorder reinstalled by a restored spy into the active recording', () => {
    const { target } = fakeConsole();
    const guard = createConsoleGuard(target);

    guard.start();
    const firstRecorder = target.error;
    guard.stop();

    guard.start();
    target.error = firstRecorder;
    target.error('logged through the reinstalled recorder');

    expect(guard.stop()).toEqual(['console.error: logged through the reinstalled recorder']);
  });

  it("drops Redux Toolkit's load-dependent middleware timing warning", () => {
    const { target, warn } = fakeConsole();
    const guard = createConsoleGuard(target);
    const timing =
      'SerializableStateInvariantMiddleware took 48ms, which is more than the warning threshold of 32ms. \n' +
      'If your state or actions are very large, you may want to disable the middleware.';

    guard.start();
    target.warn(timing);
    target.warn(
      'ImmutableStateInvariantMiddleware took 40ms, which is more than the warning threshold of 32ms.',
    );

    expect(guard.stop()).toEqual([]);
    expect(warn).not.toHaveBeenCalled();
  });

  it('still records the timing text through console.error and any other slow-operation warning', () => {
    const { target } = fakeConsole();
    const guard = createConsoleGuard(target);
    const timing =
      'SerializableStateInvariantMiddleware took 48ms, which is more than the warning threshold of 32ms.';

    guard.start();
    target.error(timing);
    target.warn('SomethingElse took 48ms, which is more than the warning threshold of 32ms.');

    expect(guard.stop()).toEqual([
      `console.error: ${timing}`,
      'console.warn: SomethingElse took 48ms, which is more than the warning threshold of 32ms.',
    ]);
  });
});

describe('failOnConsole', () => {
  it('passes a test that logs nothing', () => {
    const { target } = fakeConsole();
    const { hooks, registrar } = captureHooks();
    failOnConsole(registrar, target);

    hooks.before?.();

    expect(() => hooks.after?.()).not.toThrow();
  });

  it('fails a test that logs a warning or an error, listing every message', () => {
    const { target } = fakeConsole();
    const { hooks, registrar } = captureHooks();
    failOnConsole(registrar, target);

    hooks.before?.();
    target.error('Each child in a list should have a unique "key" prop.');
    target.warn('MUI: deprecated prop');

    expect(() => hooks.after?.()).toThrow(
      /logged 2 console warning\(s\) or error\(s\)[\s\S]*console\.error: Each child[\s\S]*console\.warn: MUI: deprecated prop/,
    );
  });

  it('ignores output a test suppresses through a mocked console method', () => {
    const { target } = fakeConsole();
    const { hooks, registrar } = captureHooks();
    failOnConsole(registrar, target);

    hooks.before?.();
    const spy = vi.spyOn(target, 'error').mockImplementation(() => {});
    target.error('expected failure log');
    expect(spy).toHaveBeenCalledWith('expected failure log');
    spy.mockRestore();

    expect(() => hooks.after?.()).not.toThrow();
  });
});
