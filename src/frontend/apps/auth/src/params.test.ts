import { describe, expect, it } from 'vitest';
import {
  describeLoginError,
  describePasswordError,
  getQueryParam,
  isAccountPath,
  isApplicationsPath,
  isConsentPath,
  isPasswordPath,
  isSessionsPath,
  readCookie,
  resolveSurfacePath,
} from './params.js';

const TENANT_BASE = 'https://op.example/acme/account/';

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

describe('resolveSurfacePath', () => {
  it('resolves a surface path against the document base by default', () => {
    expect(resolveSurfacePath('me')).toBe('/account/me');
    expect(resolveSurfacePath('./')).toBe('/account/');
  });

  it('resolves a surface path beneath the tenant the base names', () => {
    expect(resolveSurfacePath('consent/details', TENANT_BASE)).toBe(
      '/acme/account/consent/details',
    );
    expect(resolveSurfacePath('./', TENANT_BASE)).toBe('/acme/account/');
  });
});

describe('surface page matching beneath a tenant', () => {
  it('matches each page beneath the tenant the base names', () => {
    expect(isConsentPath('/acme/account/consent', TENANT_BASE)).toBe(true);
    expect(isAccountPath('/acme/account', TENANT_BASE)).toBe(true);
    expect(isAccountPath('/acme/account/', TENANT_BASE)).toBe(true);
    expect(isApplicationsPath('/acme/account/applications', TENANT_BASE)).toBe(true);
    expect(isSessionsPath('/acme/account/sessions/', TENANT_BASE)).toBe(true);
  });

  it('does not match a page of another tenant', () => {
    expect(isConsentPath('/account/consent', TENANT_BASE)).toBe(false);
    expect(isAccountPath('/beta/account', TENANT_BASE)).toBe(false);
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

describe('isPasswordPath', () => {
  it('matches the password path with trailing slashes', () => {
    expect(isPasswordPath('/account/password')).toBe(true);
    expect(isPasswordPath('/account/password/')).toBe(true);
  });

  it('does not match the account root or other paths', () => {
    expect(isPasswordPath('/account')).toBe(false);
    expect(isPasswordPath('/account/sessions')).toBe(false);
  });
});

describe('describeLoginError', () => {
  it('maps known codes and defaults unknown ones', () => {
    expect(describeLoginError(null)).toBeNull();
    expect(describeLoginError('invalid_credentials')).toMatch(/incorrect/i);
    expect(describeLoginError('invalid_request')).toMatch(/username and a password/i);
    expect(describeLoginError('access_denied')).toMatch(/cannot sign in/i);
    expect(describeLoginError('boom')).toMatch(/failed/i);
  });
});

describe('describePasswordError', () => {
  it('maps known codes and defaults unknown ones', () => {
    expect(describePasswordError(null)).toBeNull();
    expect(describePasswordError('invalid_request')).toBe(
      'Enter your current password and a new password.',
    );
    expect(describePasswordError('password_mismatch')).toBe('The new passwords do not match.');
    expect(describePasswordError('invalid_current_password')).toBe(
      'The current password is incorrect.',
    );
    expect(describePasswordError('not_supported')).toBe(
      'Your account has no password to change here.',
    );
    expect(describePasswordError('boom')).toBe(
      'The password could not be changed. Please try again.',
    );
  });
});
