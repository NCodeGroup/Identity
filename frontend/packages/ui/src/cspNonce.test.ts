import { describe, expect, it } from 'vitest';
import { readCspNonce } from './cspNonce.js';

/** Builds a detached document whose head holds a nonce `<meta>` element, and returns both. */
function documentWithNonceMeta(): { doc: Document; meta: HTMLMetaElement } {
  const doc = document.implementation.createHTMLDocument('page');
  const meta = doc.createElement('meta');
  meta.setAttribute('property', 'csp-nonce');
  doc.head.append(meta);
  return { doc, meta };
}

describe('readCspNonce', () => {
  it('resolves to undefined when the page has no nonce element', () => {
    expect(readCspNonce(document.implementation.createHTMLDocument('page'))).toBeUndefined();
  });

  it('resolves to undefined when the element carries no nonce property', () => {
    const { doc } = documentWithNonceMeta();

    expect(readCspNonce(doc)).toBeUndefined();
  });

  it('resolves to undefined when the nonce is empty', () => {
    const { doc, meta } = documentWithNonceMeta();
    // jsdom has no nonce property; a browser exposes the host's nonce through it.
    meta.nonce = '';

    expect(readCspNonce(doc)).toBeUndefined();
  });

  it("reads the nonce from the element's property", () => {
    const { doc, meta } = documentWithNonceMeta();
    // jsdom has no nonce property; a browser exposes the host's nonce through it.
    meta.nonce = 'n0nce';

    expect(readCspNonce(doc)).toBe('n0nce');
  });
});
