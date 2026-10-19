import { render, screen, within } from '@testing-library/react';
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

  describe('category summary', () => {
    const mixed: CoverageReport = {
      categories: [
        {
          id: 'tokens',
          name: 'Token security',
          entries: [
            {
              id: 'tok-dpop',
              feature: 'DPoP sender-constrained tokens',
              reference: 'RFC 9449',
              status: 'partial',
              detail: 'Advertised for a subset of algorithms.',
              channel: 'external-suite',
            },
            {
              id: 'tok-alg',
              feature: 'Asymmetric ID token signing',
              reference: 'OIDC Core §10.1',
              status: 'implemented',
              detail: 'PS256 is advertised.',
              channel: 'browser',
            },
            {
              id: 'tok-implicit',
              feature: 'No implicit grant',
              reference: 'RFC 9700 §2.1.2',
              status: 'non-conformant',
              detail: "'implicit' is advertised.",
              channel: 'browser',
            },
          ],
        },
        {
          id: 'discovery',
          name: 'Discovery',
          entries: [
            {
              id: 'disc-jwks',
              feature: 'JWKS endpoint',
              reference: 'OIDC Discovery §3',
              status: 'implemented',
              detail: "'jwks_uri' is advertised.",
              channel: 'probe-runner',
            },
          ],
        },
      ],
      totals: { implemented: 2, partial: 1, missing: 0, 'non-conformant': 1 },
    };

    function summaryButton(categoryId: string): HTMLElement {
      return within(screen.getByTestId(`coverage-category-${categoryId}`)).getAllByRole(
        'button',
      )[0]!;
    }

    it('counts each status present and omits the absent ones', () => {
      render(<CoverageMatrix report={mixed} />);
      const header = summaryButton('tokens');

      expect(header).toHaveTextContent('1 non-conformant');
      expect(header).toHaveTextContent('1 partial');
      expect(header).toHaveTextContent('1 implemented');
      expect(header).not.toHaveTextContent('missing');
      expect(summaryButton('discovery')).toHaveTextContent(/^Discovery1 implemented$/);
    });

    it('expands a category with a non-conformant gap and collapses a complete one', () => {
      render(<CoverageMatrix report={mixed} />);

      expect(summaryButton('tokens')).toHaveAttribute('aria-expanded', 'true');
      expect(summaryButton('discovery')).toHaveAttribute('aria-expanded', 'false');
    });

    it('orders entries worst-status-first and labels each channel', () => {
      render(<CoverageMatrix report={mixed} />);
      const text = screen.getByTestId('coverage-category-tokens').textContent!;

      expect(text.indexOf('No implicit grant')).toBeLessThan(
        text.indexOf('DPoP sender-constrained tokens'),
      );
      expect(text.indexOf('DPoP sender-constrained tokens')).toBeLessThan(
        text.indexOf('Asymmetric ID token signing'),
      );
      expect(screen.getByTestId('channel-external-suite')).toHaveTextContent('external suite');
      expect(screen.getByTestId('channel-probe-runner')).toHaveTextContent('probe runner');
      expect(screen.queryByTestId(/^backlog-/)).not.toBeInTheDocument();
    });
  });
});
