import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act } from '@testing-library/react';
import type { Root } from 'react-dom/client';
import { mountSetup } from './mount.js';
import { getStatus } from './setupApi.js';

vi.mock('./setupApi.js', () => ({ getStatus: vi.fn(), createRoot: vi.fn() }));

describe('mountSetup', () => {
  let container: HTMLElement;
  let root: Root | undefined;

  beforeEach(() => {
    vi.mocked(getStatus).mockResolvedValue({
      kind: 'ok',
      status: { tokenSource: 'configured', minimumPasswordLength: 15 },
    });
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

  async function mount(): Promise<void> {
    await act(async () => {
      root = mountSetup(container);
      await Promise.resolve();
    });
  }

  it('renders the wizard into the container', async () => {
    await mount();

    expect(container.textContent).toContain('Set up NCode.Identity');
  });

  it('gives the styles it inserts the nonce the host wrote into the page', async () => {
    const meta = document.createElement('meta');
    meta.setAttribute('property', 'csp-nonce');
    // jsdom has no nonce property; a browser exposes the host's nonce through it.
    meta.nonce = 'n0nce';
    document.head.append(meta);

    await mount();

    const styles = Array.from(document.head.querySelectorAll('style[data-emotion]'));
    expect(styles.length).toBeGreaterThan(0);
    expect(styles.map((s) => s.getAttribute('nonce'))).toEqual(styles.map(() => 'n0nce'));
  });
});
