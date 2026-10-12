import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CodeBlock } from './CodeBlock.js';

describe('CodeBlock', () => {
  it('renders the value and an optional label', () => {
    const { getByText, getByTestId } = render(
      <CodeBlock label="Authorize URL" value="https://op.test/authorize?x=1" />,
    );
    expect(getByText('Authorize URL')).toBeInTheDocument();
    expect(getByTestId('code-block').textContent).toBe('https://op.test/authorize?x=1');
  });
});
