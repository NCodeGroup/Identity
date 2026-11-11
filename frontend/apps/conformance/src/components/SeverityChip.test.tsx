import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SeverityChip } from './SeverityChip.js';

describe('SeverityChip', () => {
  it('renders the label and test id for each severity', () => {
    const { getByTestId, getByText } = render(<SeverityChip severity="fail" />);
    expect(getByTestId('severity-fail')).toBeInTheDocument();
    expect(getByText('Fail')).toBeInTheDocument();
  });
});
