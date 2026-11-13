import { afterEach, describe, expect, it } from 'vitest';
import { takeSetupToken } from './setupToken.js';

describe('takeSetupToken', () => {
  afterEach(() => {
    window.history.replaceState(null, '', '/');
  });

  it('reads the decoded token and removes the fragment', () => {
    window.history.replaceState(null, '', '/setup/?a=1#token=ab%2Bc%2Fd%3D');

    expect(takeSetupToken()).toBe('ab+c/d=');
    expect(window.location.pathname).toBe('/setup/');
    expect(window.location.search).toBe('?a=1');
    expect(window.location.hash).toBe('');
  });

  it('keeps the other fragment parameters', () => {
    window.history.replaceState(null, '', '/setup/#lang=en&token=t0ken&x=y');

    expect(takeSetupToken()).toBe('t0ken');
    expect(window.location.hash).toBe('#lang=en&x=y');
  });

  it('yields undefined and leaves the URL when the fragment has no token', () => {
    window.history.replaceState(null, '', '/setup/?a=1#lang=en');

    expect(takeSetupToken()).toBeUndefined();
    expect(window.location.href).toContain('/setup/?a=1#lang=en');
  });

  it('yields undefined when there is no fragment', () => {
    window.history.replaceState(null, '', '/setup/');

    expect(takeSetupToken()).toBeUndefined();
  });

  it('reads a fragment that does not start with a hash sign', () => {
    const replaced: string[] = [];
    const win = {
      location: { hash: 'token=abc', pathname: '/setup/', search: '' },
      history: {
        state: null,
        replaceState: (_state: unknown, _title: string, url: string) => {
          replaced.push(url);
        },
      },
    } as unknown as Window;

    expect(takeSetupToken(win)).toBe('abc');
    expect(replaced).toEqual(['/setup/']);
  });
});
