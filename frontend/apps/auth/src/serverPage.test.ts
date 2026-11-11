import { afterEach, describe, expect, it } from 'vitest';
import { EMPTY_SERVER_PAGE, readServerPage, writeCarriedValue } from './serverPage.js';

const containers: HTMLElement[] = [];

/** Attaches a container to the document, since focus and `contains` only apply to attached nodes. */
function pageOf(html: string): HTMLElement {
  const container = document.createElement('div');
  container.innerHTML = html;
  document.body.append(container);
  containers.push(container);
  return container;
}

function inputOf(container: HTMLElement, id: string): HTMLInputElement {
  const input = container.querySelector<HTMLInputElement>(`#${id}`);
  expect(input).not.toBeNull();
  return input!;
}

afterEach(() => {
  for (const container of containers.splice(0)) {
    container.remove();
  }
});

describe('readServerPage', () => {
  it('reads the values typed or autofilled into the text, email, and password fields by id', () => {
    const container = pageOf(
      `<form>
        <input id="username" type="text">
        <input id="email" type="email">
        <input id="password" type="password">
        <input id="empty" type="text">
        <input id="token" type="hidden" value="abc">
        <input id="remember" type="checkbox" value="yes" checked>
        <input type="text" value="no id">
      </form>`,
    );
    inputOf(container, 'username').value = 'alice';
    inputOf(container, 'email').value = 'alice@example.com';
    inputOf(container, 'password').value = 's3cret';

    const page = readServerPage(container, null);

    expect(page.fieldValues).toEqual({
      username: 'alice',
      email: 'alice@example.com',
      password: 's3cret',
    });
  });

  it('lists the consent scopes the end-user cleared', () => {
    const container = pageOf(
      `<form>
        <input type="checkbox" name="scope" value="openid" checked>
        <input type="checkbox" name="scope" value="profile">
        <input type="checkbox" name="scope" value="email">
        <input type="checkbox" name="other" value="ignored">
      </form>`,
    );

    const page = readServerPage(container, null);

    expect(page.clearedScopes).toEqual(['profile', 'email']);
  });

  it('names the focused field only when it lies inside the page', () => {
    const container = pageOf('<input id="password" type="password"><input type="text">');
    const outside = pageOf('<input id="search" type="text">');
    const password = inputOf(container, 'password');
    const withoutId = container.querySelector<HTMLInputElement>('input:not([id])')!;

    expect(readServerPage(container, password).focusedFieldId).toBe('password');
    expect(readServerPage(container, inputOf(outside, 'search')).focusedFieldId).toBeNull();
    expect(readServerPage(container, null).focusedFieldId).toBeNull();
    expect(readServerPage(container, withoutId).focusedFieldId).toBeNull();
    expect(readServerPage(container, container).focusedFieldId).toBeNull();
  });

  it('reads the authorization error and its description', () => {
    const container = pageOf(
      `<p id="authorization-error" role="alert" data-error-code="invalid_client">Client unknown</p>
       <p id="authorization-error-description">The client is not registered.</p>`,
    );

    const page = readServerPage(container, null);

    expect(page.authorizationError).toEqual({
      code: 'invalid_client',
      description: 'The client is not registered.',
    });
  });

  it('falls back to the error element text for a missing code attribute and to no description', () => {
    const container = pageOf('<p id="authorization-error" role="alert">server_error</p>');

    const page = readServerPage(container, null);

    expect(page.authorizationError).toEqual({ code: 'server_error', description: null });
  });

  it('reads a page without input as the empty state', () => {
    const container = pageOf('<p>Nothing to carry.</p>');

    expect(readServerPage(container, null)).toEqual(EMPTY_SERVER_PAGE);
  });
});

describe('writeCarriedValue', () => {
  it('writes the value through the property and leaves the attribute unset', () => {
    const input = document.createElement('input');
    input.type = 'password';

    writeCarriedValue(input, 's3cret');

    expect(input.value).toBe('s3cret');
    expect(input.hasAttribute('value')).toBe(false);
  });

  it('leaves an input that already holds a value', () => {
    const input = document.createElement('input');
    input.value = 'typed';

    writeCarriedValue(input, 'carried');

    expect(input.value).toBe('typed');
  });

  it('ignores a detached ref and a value that was not carried', () => {
    const input = document.createElement('input');

    expect(() => {
      writeCarriedValue(null, 'carried');
      writeCarriedValue(input, undefined);
    }).not.toThrow();
    expect(input.value).toBe('');
  });
});
