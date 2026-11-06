import { afterEach, describe, expect, it, vi } from 'vitest';
import type { BffRuntimeConfig } from '@ncode/config';
import type { BffUser } from '@ncode/store';
import {
  SESSION_CHANNEL_NAME,
  SESSION_ENDED_MESSAGE,
  SESSION_ENDED_NOTICE,
  SESSION_POLL_INTERVAL_MS,
  announceSessionEnded,
  loginUrl,
  nextProbeDelayMs,
  openSessionChannel,
  readSignInError,
  signInErrorMessage,
  submitLogout,
} from './session.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: '/management',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const user: BffUser = {
  sub: 'u1',
  claims: {},
  session: { expiresAt: 2_000, idleExpiresAt: 1_000 },
  logoutUrl: '/bff/logout',
  logoutSid: 'sid-1',
};

class FakeBroadcastChannel {
  static instances: FakeBroadcastChannel[] = [];
  readonly posted: unknown[] = [];
  closed = false;
  onmessage: ((event: MessageEvent<unknown>) => void) | null = null;

  constructor(readonly name: string) {
    FakeBroadcastChannel.instances.push(this);
  }

  postMessage(message: unknown): void {
    this.posted.push(message);
  }

  close(): void {
    this.closed = true;
  }

  deliver(data: unknown): void {
    this.onmessage?.({ data } as MessageEvent<unknown>);
  }
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  FakeBroadcastChannel.instances = [];
  document.body.innerHTML = '';
});

describe('constants', () => {
  it('polls every minute', () => {
    expect(SESSION_POLL_INTERVAL_MS).toBe(60_000);
  });

  it('names the session channel and its message', () => {
    expect(SESSION_CHANNEL_NAME).toBe('ncode.mgmt.session');
    expect(SESSION_ENDED_MESSAGE).toBe('ended');
  });

  it('words the notice a session end shows', () => {
    expect(SESSION_ENDED_NOTICE).toBe('Your session ended. Sign in to continue.');
  });
});

describe('loginUrl', () => {
  it('targets the BFF login and encodes the return path', () => {
    expect(loginUrl(runtime, '/teams/acme?tab=1')).toBe(
      '/bff/login?returnUrl=%2Fteams%2Facme%3Ftab%3D1',
    );
  });

  it('follows the BFF base path', () => {
    const other: BffRuntimeConfig = {
      ...runtime,
      bff: { ...runtime.bff, basePath: '/auth' },
    };

    expect(loginUrl(other, '/')).toBe('/auth/login?returnUrl=%2F');
  });
});

describe('submitLogout', () => {
  it('posts a hidden form with the session id and the root return URL', () => {
    const submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {});

    submitLogout(user);

    expect(submit).toHaveBeenCalledTimes(1);
    const form = document.body.querySelector('form');
    expect(form).not.toBeNull();
    expect(form?.method).toBe('post');
    expect(form?.getAttribute('action')).toBe('/bff/logout');
    expect(form?.hidden).toBe(true);
    const fields = Array.from(form?.querySelectorAll('input') ?? []).map((input) => [
      input.type,
      input.name,
      input.value,
    ]);
    expect(fields).toEqual([
      ['hidden', 'sid', 'sid-1'],
      ['hidden', 'returnUrl', '/'],
    ]);
  });

  it('appends the form to the given document', () => {
    vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {});
    const other = document.implementation.createHTMLDocument('other');

    submitLogout(user, other);

    expect(other.body.querySelector('form')).not.toBeNull();
    expect(document.body.querySelector('form')).toBeNull();
  });
});

describe('readSignInError', () => {
  it('reads the error code', () => {
    expect(readSignInError('?error=access_denied')).toBe('access_denied');
  });

  it('is undefined without an error', () => {
    expect(readSignInError('')).toBeUndefined();
    expect(readSignInError('?other=1')).toBeUndefined();
  });

  it('is undefined for an empty error', () => {
    expect(readSignInError('?error=')).toBeUndefined();
  });
});

describe('signInErrorMessage', () => {
  it('names the code', () => {
    expect(signInErrorMessage('access_denied')).toBe('Sign-in failed (access_denied).');
  });
});

describe('nextProbeDelayMs', () => {
  it('waits one second past the idle expiry when it comes first', () => {
    expect(nextProbeDelayMs(user, 400_000)).toBe(601_000);
  });

  it('waits one second past the absolute expiry when it comes first', () => {
    const absolute: BffUser = { ...user, session: { expiresAt: 500, idleExpiresAt: 1_000 } };

    expect(nextProbeDelayMs(absolute, 100_000)).toBe(401_000);
  });

  it('waits one second once the expiry has passed', () => {
    expect(nextProbeDelayMs(user, 5_000_000)).toBe(1_000);
  });

  it('reads the clock by default', () => {
    vi.spyOn(Date, 'now').mockReturnValue(999_000);

    expect(nextProbeDelayMs(user)).toBe(2_000);
  });
});

describe('openSessionChannel', () => {
  it('calls back when another tab announces an ended session', () => {
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);
    const onEnded = vi.fn();

    openSessionChannel(onEnded);

    const channel = FakeBroadcastChannel.instances[0];
    expect(channel?.name).toBe(SESSION_CHANNEL_NAME);
    channel?.deliver(SESSION_ENDED_MESSAGE);
    expect(onEnded).toHaveBeenCalledTimes(1);
  });

  it('ignores other messages', () => {
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);
    const onEnded = vi.fn();

    openSessionChannel(onEnded);
    FakeBroadcastChannel.instances[0]?.deliver('other');

    expect(onEnded).not.toHaveBeenCalled();
  });

  it('closes the channel', () => {
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);

    openSessionChannel(vi.fn()).close();

    expect(FakeBroadcastChannel.instances[0]?.closed).toBe(true);
  });

  it('does nothing without BroadcastChannel', () => {
    vi.stubGlobal('BroadcastChannel', undefined);
    const onEnded = vi.fn();

    const handle = openSessionChannel(onEnded);
    handle.close();

    expect(onEnded).not.toHaveBeenCalled();
    expect(FakeBroadcastChannel.instances).toHaveLength(0);
  });
});

describe('announceSessionEnded', () => {
  it('posts the ended message and closes the channel', () => {
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);

    announceSessionEnded();

    const channel = FakeBroadcastChannel.instances[0];
    expect(channel?.name).toBe(SESSION_CHANNEL_NAME);
    expect(channel?.posted).toEqual([SESSION_ENDED_MESSAGE]);
    expect(channel?.closed).toBe(true);
  });

  it('does nothing without BroadcastChannel', () => {
    vi.stubGlobal('BroadcastChannel', undefined);

    expect(() => announceSessionEnded()).not.toThrow();
    expect(FakeBroadcastChannel.instances).toHaveLength(0);
  });
});
