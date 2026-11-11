import { describe, expect, it } from 'vitest';
import { formatConsoleArgs } from './formatConsoleArgs.js';

describe('formatConsoleArgs', () => {
  it('substitutes string specifiers in order', () => {
    expect(
      formatConsoleArgs(['An update to %s inside a test was not wrapped in act(...).', 'Dialog']),
    ).toBe('An update to Dialog inside a test was not wrapped in act(...).');
  });

  it('renders integer and float specifiers numerically', () => {
    expect(formatConsoleArgs(['%d of %i at %f', '3.7', 9.9, '1.5'])).toBe('3 of 9 at 1.5');
    expect(formatConsoleArgs(['big %d', 12n])).toBe('big 12');
  });

  it('serializes object specifiers and drops %c styling', () => {
    expect(formatConsoleArgs(['%o %O %j%c!', { a: 1 }, [2], { b: 'c' }, 'color: red'])).toBe(
      '{"a":1} [2] {"b":"c"}!',
    );
  });

  it('keeps a literal percent and a specifier with no argument left', () => {
    expect(formatConsoleArgs(['100%% of %s and %s', 'one'])).toBe('100% of one and %s');
  });

  it('appends arguments beyond the format string', () => {
    expect(formatConsoleArgs(['prefix %s', 'a', 'b', 3])).toBe('prefix a b 3');
  });

  it('joins non-string leading arguments', () => {
    expect(formatConsoleArgs([42, null, undefined, true])).toBe('42 null undefined true');
    expect(formatConsoleArgs([])).toBe('');
  });

  it('prints an error with its stack, or its name and message without one', () => {
    const withStack = new Error('boom');
    expect(formatConsoleArgs([withStack])).toBe(withStack.stack);

    const withoutStack = new TypeError('bad');
    Object.defineProperty(withoutStack, 'stack', { value: undefined });
    expect(formatConsoleArgs(['failed:', withoutStack])).toBe('failed: TypeError: bad');
  });

  it('falls back to the object tag for a value JSON cannot serialize', () => {
    const circular: Record<string, unknown> = {};
    circular['self'] = circular;
    expect(formatConsoleArgs(['%o', circular])).toBe('[object Object]');
  });
});
