import { render, within } from '@testing-library/react';
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

  it.each([
    { status: 'idle', label: 'Pending' },
    { status: 'active', label: 'Ready' },
    { status: 'done', label: 'Done' },
    { status: 'error', label: 'Error' },
  ] as const)('labels the $status status as "$label"', ({ status, label }) => {
    const { getByTestId } = render(<StepCard index={1} title="Build request" status={status} />);
    const card = getByTestId('step-1');
    expect(card).toHaveAttribute('data-status', status);
    expect(within(card).getByText(label)).toBeInTheDocument();
  });

  it('renders only the header when there is no description or content', () => {
    const { getByTestId } = render(<StepCard index={3} title="Use the tokens" status="idle" />);
    const card = getByTestId('step-3');
    expect(within(card).getByRole('heading', { name: 'Use the tokens' })).toBeInTheDocument();
    // The title is an h2; a description would be the only paragraph.
    expect(card.querySelector('p')).toBeNull();
  });

  it('renders the supplied content', () => {
    const { getByRole } = render(
      <StepCard index={1} title="Build request" status="active">
        <button type="button">Start authorization</button>
      </StepCard>,
    );
    expect(getByRole('button', { name: 'Start authorization' })).toBeInTheDocument();
  });
});
