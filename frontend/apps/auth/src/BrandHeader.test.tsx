import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { BrandHeader } from './BrandHeader.js';

describe('BrandHeader', () => {
  it('draws the logo with the display name as its text alternative', () => {
    const { container } = render(
      <BrandHeader
        branding={{ displayName: 'Acme', logoUrl: 'https://cdn.acme.example/logo.svg' }}
      />,
    );

    const logo = container.querySelector('header img');
    expect(logo?.getAttribute('src')).toBe('https://cdn.acme.example/logo.svg');
    expect(logo?.getAttribute('alt')).toBe('Acme');
  });

  it('marks a logo without a display name as decorative', () => {
    const { container } = render(
      <BrandHeader branding={{ logoUrl: 'https://cdn.acme.example/logo.svg' }} />,
    );

    expect(container.querySelector('header img')?.getAttribute('alt')).toBe('');
  });

  it('draws nothing when the tenant has no logo', () => {
    const { container } = render(<BrandHeader branding={{ displayName: 'Acme' }} />);

    expect(container.firstChild).toBeNull();
  });
});
