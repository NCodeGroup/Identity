import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CoverageStatusChip } from './CoverageStatusChip.js';

describe('CoverageStatusChip', () => {
  it('renders the label and test id for each status', () => {
    const { getByTestId, getByText } = render(<CoverageStatusChip status="missing" />);
    expect(getByTestId('coverage-missing')).toBeInTheDocument();
    expect(getByText('Missing')).toBeInTheDocument();
  });

  it('renders the non-conformant status', () => {
    const { getByTestId, getByText } = render(<CoverageStatusChip status="non-conformant" />);
    expect(getByTestId('coverage-non-conformant')).toBeInTheDocument();
    expect(getByText('Non-conformant')).toBeInTheDocument();
  });
});
