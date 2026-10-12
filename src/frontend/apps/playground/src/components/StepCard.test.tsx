import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StepCard } from './StepCard.js';

describe('StepCard', () => {
  it('renders the title, index, and status', () => {
    const { getByTestId, getByText } = render(
      <StepCard index={2} title="Exchange code" status="done" description="Swap the code." />,
    );
    const card = getByTestId('step-2');
    expect(card).toHaveAttribute('data-status', 'done');
    expect(getByText('Exchange code')).toBeInTheDocument();
    expect(getByText('Swap the code.')).toBeInTheDocument();
    expect(getByText('Done')).toBeInTheDocument();
  });
});
