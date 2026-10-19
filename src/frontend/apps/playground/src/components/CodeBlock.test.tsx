import { render } from '@testing-library/react';
import { createTheme } from '@mui/material/styles';
import { describe, expect, it } from 'vitest';
import { CodeBlock } from './CodeBlock.js';

// Rendered without a provider, the block resolves its palette tokens against MUI's default theme.
const defaultTheme = createTheme();

describe('CodeBlock', () => {
  it('renders the value and an optional label', () => {
    const { getByText, getByTestId } = render(
      <CodeBlock label="Authorize URL" value="https://op.test/authorize?x=1" />,
    );
    expect(getByText('Authorize URL')).toBeInTheDocument();
    expect(getByTestId('code-block').textContent).toBe('https://op.test/authorize?x=1');
  });

  it('renders only the block when no label is given', () => {
    const { getByTestId } = render(<CodeBlock value="opaque-token" />);
    const block = getByTestId('code-block');
    expect(block).toHaveTextContent('opaque-token');
    expect(block.parentElement?.children).toHaveLength(1);
  });

  it('tints a sensitive value with the warning border', () => {
    const { getByTestId } = render(<CodeBlock value="refresh-token" sensitive />);
    expect(getByTestId('code-block')).toHaveStyle({
      borderTopColor: defaultTheme.palette.warning.light,
    });
  });

  it('renders a non-sensitive value with the neutral divider border', () => {
    const { getByTestId } = render(<CodeBlock value="https://op.test" />);
    expect(getByTestId('code-block')).toHaveStyle({ borderTopColor: defaultTheme.palette.divider });
  });
});
