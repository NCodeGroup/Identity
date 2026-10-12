import { describe, expect, it } from 'vitest';
import {
  describeLoginError,
  getQueryParam,
  isAccountPath,
  isApplicationsPath,
  isConsentPath,
  readCookie,
} from './params.js';

describe('getQueryParam', () => {
  it('reads a value with or without a leading question mark', () => {
    expect(getQueryParam('?ReturnUrl=%2Fauthorize%3Fx%3D1', 'ReturnUrl')).toBe('/authorize?x=1');
    expect(getQueryParam('error=invalid_credentials', 'error')).toBe('invalid_credentials');
  });

  it('returns null when absent', () => {
    expect(getQueryParam('?a=b', 'missing')).toBeNull();
  });
});

describe('readCookie', () => {
  it('reads a cookie value and url-decodes it', () => {
    expect(readCookie('a=1; XSRF-TOKEN=tok%2Fen; b=2', 'XSRF-TOKEN')).toBe('tok/en');
  });

  it('returns null when the cookie is absent', () => {
    expect(readCookie('a=1; b=2', 'XSRF-TOKEN')).toBeNull();
  });
});

describe('isConsentPath', () => {
  it('matches the consent path with trailing slashes', () => {
    expect(isConsentPath('/account/consent')).toBe(true);
    expect(isConsentPath('/account/consent/')).toBe(true);
  });

  it('does not match the login path', () => {
    expect(isConsentPath('/account/login')).toBe(false);
  });
});

describe('isAccountPath', () => {
  it('matches the account root with trailing slashes', () => {
    expect(isAccountPath('/account')).toBe(true);
    expect(isAccountPath('/account/')).toBe(true);
  });

  it('does not match the login or consent paths', () => {
    expect(isAccountPath('/account/login')).toBe(false);
    expect(isAccountPath('/account/consent')).toBe(false);
  });
});

describe('isApplicationsPath', () => {
  it('matches the applications path with trailing slashes', () => {
    expect(isApplicationsPath('/account/applications')).toBe(true);
    expect(isApplicationsPath('/account/applications/')).toBe(true);
  });

  it('does not match the account root or other paths', () => {
    expect(isApplicationsPath('/account')).toBe(false);
    expect(isApplicationsPath('/account/consent')).toBe(false);
  });
});

describe('describeLoginError', () => {
  it('maps known codes and defaults unknown ones', () => {
    expect(describeLoginError(null)).toBeNull();
    expect(describeLoginError('invalid_credentials')).toMatch(/incorrect/i);
    expect(describeLoginError('invalid_request')).toMatch(/username and a password/i);
    expect(describeLoginError('boom')).toMatch(/failed/i);
  });
});
