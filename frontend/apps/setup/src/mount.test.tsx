import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act } from '@testing-library/react';
import type { Root } from 'react-dom/client';
import { mountSetup } from './mount.js';

describe('mountSetup', () => {
  let container: HTMLElement;
  let root: Root | undefined;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.append(container);
  });

  afterEach(() => {
    act(() => {
      root?.unmount();
    });
    root = undefined;
    container.remove();
    document.head
      .querySelectorAll('meta[property="csp-nonce"], style[data-emotion]')
      .forEach((element) => {
        element.remove();
      });
  });

  function mount(): void {
    act(() => {
      root = mountSetup(container);
    });
  }

  it('renders the wizard into the container', () => {
    mount();

    expect(container.textContent).toContain('NCode.Identity setup');
  });

  it('gives the styles it inserts the nonce the host wrote into the page', () => {
    const meta = document.createElement('meta');
    meta.setAttribute('property', 'csp-nonce');
    // jsdom has no nonce property; a browser exposes the host's nonce through it.
    meta.nonce = 'n0nce';
    document.head.append(meta);

    mount();

    const styles = Array.from(document.head.querySelectorAll('style[data-emotion]'));
    expect(styles.length).toBeGreaterThan(0);
    expect(styles.map((s) => s.getAttribute('nonce'))).toEqual(styles.map(() => 'n0nce'));
  });
});
