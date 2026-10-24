import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act } from '@testing-library/react';
import type { Root } from 'react-dom/client';
import { mountSurface } from './mount.js';

describe('mountSurface', () => {
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
    document.getElementById('branding')?.remove();
  });

  function mount(): void {
    act(() => {
      root = mountSurface(container);
    });
  }

  it('keeps what the end-user typed into the server-rendered login form', () => {
    container.innerHTML = `<form>
      <input id="username" type="text">
      <input id="password" type="password">
    </form>`;
    container.querySelector<HTMLInputElement>('#username')!.value = 'alice';
    const serverPassword = container.querySelector<HTMLInputElement>('#password')!;
    serverPassword.value = 's3cret';
    serverPassword.focus();

    mount();

    const username = container.querySelector<HTMLInputElement>('#username');
    const password = container.querySelector<HTMLInputElement>('#password');
    expect(username).not.toBe(serverPassword);
    expect(username?.value).toBe('alice');
    expect(password).not.toBe(serverPassword);
    expect(password?.value).toBe('s3cret');
    expect(password).not.toHaveAttribute('value');
    expect(password).toHaveFocus();
  });

  it('themes the surface with the branding the host embedded', () => {
    const block = document.createElement('script');
    block.type = 'application/json';
    block.id = 'branding';
    block.textContent = '{"displayName":"Acme"}';
    document.head.append(block);

    mount();

    expect(container.textContent).toContain('Sign in to Acme');
  });

  it('redraws the authorization error the host rendered', () => {
    container.innerHTML =
      '<p id="authorization-error" role="alert" data-error-code="invalid_client">invalid_client</p>';

    mount();

    expect(container.querySelector('#authorization-error')).toHaveAttribute(
      'data-error-code',
      'invalid_client',
    );
    expect(container.querySelector('#username')).toBeNull();
  });
});
