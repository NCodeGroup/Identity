import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { CoverageReport } from '@ncode/conformance-core';
import { CoverageMatrix } from './CoverageMatrix.js';

const report: CoverageReport = {
  categories: [
    {
      id: 'endpoints',
      name: 'Protocol endpoints',
      entries: [
        {
          id: 'ep-authorize',
          feature: 'Authorization endpoint',
          reference: 'RFC 6749 §3.1',
          status: 'implemented',
          detail: "'authorization_endpoint' is advertised.",
          channel: 'browser',
        },
        {
          id: 'ep-ciba',
          feature: 'CIBA backchannel authentication',
          reference: 'OIDC CIBA Core §4',
          status: 'missing',
          detail: "'backchannel_authentication_endpoint' is not advertised.",
          channel: 'probe-runner',
          backlog: 'F3',
        },
      ],
    },
  ],
  totals: { implemented: 1, partial: 0, missing: 1, 'non-conformant': 0 },
};

describe('CoverageMatrix', () => {
  it('renders each category and entry with status, channel, and backlog chips', () => {
    const { getByTestId, getByText } = render(<CoverageMatrix report={report} />);
    expect(getByTestId('coverage-category-endpoints')).toBeInTheDocument();
    expect(getByText('Authorization endpoint')).toBeInTheDocument();
    expect(getByText('CIBA backchannel authentication')).toBeInTheDocument();
    expect(getByTestId('backlog-F3')).toBeInTheDocument();
    expect(getByTestId('channel-probe-runner')).toBeInTheDocument();
  });

  it('orders the missing entry before the implemented one', () => {
    const { getByTestId } = render(<CoverageMatrix report={report} />);
    const html = getByTestId('coverage-category-endpoints').innerHTML;
    expect(html.indexOf('CIBA backchannel authentication')).toBeLessThan(
      html.indexOf('Authorization endpoint'),
    );
  });
});
