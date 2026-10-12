import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from './App.js';

describe('Conformance App', () => {
  it('renders the surface heading', () => {
    const { getByRole } = render(<App />);
    expect(getByRole('heading', { name: /NCode\.Identity Conformance/i })).toBeInTheDocument();
  });
});
