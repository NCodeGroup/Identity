import { describe, expect, it } from 'vitest';
import type { ProbeReport } from '@ncode/conformance-core';
import { exitCode, formatReport, toJson } from './report.js';

const report: ProbeReport = {
  results: [
    {
      id: 'runner-client-credentials',
      feature: 'client_credentials grant issues an access token',
      outcome: 'verified',
      detail: 'Issued an access token.',
      reference: 'RFC 6749 §4.4',
    },
    {
      id: 'runner-ciba',
      feature: 'CIBA backchannel authentication issues an auth_req_id',
      outcome: 'skipped',
      detail: "'backchannel_authentication_endpoint' is not advertised (gap: #41).",
      reference: 'OpenID Connect CIBA Core §7.3',
    },
  ],
  totals: { verified: 1, failed: 0, skipped: 1 },
};

describe('formatReport', () => {
  it('renders each result and a totals line', () => {
    const text = formatReport(report, 'https://op.test');
    expect(text).toContain('https://op.test');
    expect(text).toContain('VERIFIED');
    expect(text).toContain('SKIPPED');
    expect(text).toContain('1 verified · 0 failed · 1 skipped');
  });
});

describe('toJson', () => {
  it('emits a stable JSON document with the issuer', () => {
    const parsed = JSON.parse(toJson(report, 'https://op.test'));
    expect(parsed.issuer).toBe('https://op.test');
    expect(parsed.totals.verified).toBe(1);
    expect(parsed.results).toHaveLength(2);
  });
});

describe('exitCode', () => {
  it('is 0 when nothing failed', () => {
    expect(exitCode(report)).toBe(0);
  });

  it('is 1 when a probe failed', () => {
    expect(exitCode({ ...report, totals: { verified: 0, failed: 1, skipped: 0 } })).toBe(1);
  });
});
