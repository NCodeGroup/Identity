import { afterEach, describe, expect, it, vi } from 'vitest';
import { takeSetupFragment } from './setupToken.js';

describe('takeSetupFragment', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('reads the decoded token and removes the fragment', () => {
    window.history.replaceState(null, '', '/setup/?a=1#token=ab%2Bc%2Fd%3D');

    expect(takeSetupFragment()).toEqual({ token: 'ab+c/d=' });
    expect(window.location.pathname).toBe('/setup/');
    expect(window.location.search).toBe('?a=1');
    expect(window.location.hash).toBe('');
  });

  it('reads the token and the console parameters and removes all three', () => {
    window.history.replaceState(
      null,
      '',
      '/setup/?a=1#token=t0ken&consoleUrl=https%3A%2F%2Fmanage.example.com&consoleClientId=mgmt-console',
    );

    expect(takeSetupFragment()).toEqual({
      token: 't0ken',
      consoleUrl: 'https://manage.example.com',
      consoleClientId: 'mgmt-console',
    });
    expect(window.location.pathname).toBe('/setup/');
    expect(window.location.search).toBe('?a=1');
    expect(window.location.hash).toBe('');
  });

  it('keeps the other fragment parameters', () => {
    window.history.replaceState(
      null,
      '',
      '/setup/#lang=en&token=t0ken&consoleUrl=https%3A%2F%2Fm.example.com&x=y&consoleClientId=c',
    );

    expect(takeSetupFragment()).toEqual({
      token: 't0ken',
      consoleUrl: 'https://m.example.com',
      consoleClientId: 'c',
    });
    expect(window.location.hash).toBe('#lang=en&x=y');
  });

  it('reads a console parameter without a token', () => {
    window.history.replaceState(null, '', '/setup/#consoleUrl=https%3A%2F%2Fm.example.com');

    expect(takeSetupFragment()).toEqual({ consoleUrl: 'https://m.example.com' });
    expect(window.location.hash).toBe('');
  });

  it('counts an empty value as absent and still removes the parameter', () => {
    window.history.replaceState(null, '', '/setup/#token=&consoleUrl=&lang=en');

    expect(takeSetupFragment()).toEqual({});
    expect(window.location.hash).toBe('#lang=en');
  });

  it('yields no values and leaves the URL when the fragment has none of the parameters', () => {
    window.history.replaceState(null, '', '/setup/?a=1#lang=en');
    const replaceState = vi.spyOn(window.history, 'replaceState');

    expect(takeSetupFragment()).toEqual({});
    expect(replaceState).not.toHaveBeenCalled();
    expect(window.location.href).toContain('/setup/?a=1#lang=en');
  });

  it('yields no values when there is no fragment', () => {
    window.history.replaceState(null, '', '/setup/');
    const replaceState = vi.spyOn(window.history, 'replaceState');

    expect(takeSetupFragment()).toEqual({});
    expect(replaceState).not.toHaveBeenCalled();
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

    expect(takeSetupFragment(win)).toEqual({ token: 'abc' });
    expect(replaced).toEqual(['/setup/']);
  });
});
