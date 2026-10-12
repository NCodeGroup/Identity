import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { Finding } from '../audit.js';
import { FindingsList } from './FindingsList.js';

const findings: readonly Finding[] = [
  { id: 'a', title: 'Passing rule', severity: 'pass', detail: 'All good.', reference: 'RFC X' },
  { id: 'b', title: 'Failing rule', severity: 'fail', detail: 'Bad.', reference: 'RFC Y' },
];

describe('FindingsList', () => {
  it('renders findings with failures first', () => {
    const { getByTestId, getByText } = render(<FindingsList findings={findings} />);
    const list = getByTestId('findings');
    expect(list.textContent).toContain('Failing rule');
    expect(list.textContent).toContain('Passing rule');
    expect(getByText('RFC Y')).toBeInTheDocument();
    // The failing finding sorts before the passing one.
    const html = list.innerHTML;
    expect(html.indexOf('Failing rule')).toBeLessThan(html.indexOf('Passing rule'));
  });
});
