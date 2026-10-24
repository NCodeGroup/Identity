import { describe, expect, it } from 'vitest';
import { BRANDING_ELEMENT_ID, readBranding } from './branding.js';

/** Builds a detached document whose head holds the given markup. */
function documentWithHead(head: string): Document {
  const doc = document.implementation.createHTMLDocument('');
  doc.head.innerHTML = head;
  return doc;
}

describe('readBranding', () => {
  it('reads the branding the host embedded and keeps its well-formed tokens', () => {
    const doc = documentWithHead(
      `<script type="application/json" id="${BRANDING_ELEMENT_ID}">{"displayName":"Acme","primaryColor":"#1565c0","mode":"bogus"}</script>`,
    );

    expect(readBranding(doc)).toEqual({ displayName: 'Acme', primaryColor: '#1565c0' });
  });

  it('applies the default presentation when the page embeds no branding', () => {
    expect(readBranding(documentWithHead(''))).toBeUndefined();
    expect(
      readBranding(documentWithHead(`<script type="application/json" id="branding"></script>`)),
    ).toBeUndefined();
  });

  it('applies the default presentation when the embedded branding is not JSON', () => {
    const doc = documentWithHead(
      `<script type="application/json" id="${BRANDING_ELEMENT_ID}">{not json</script>`,
    );

    expect(readBranding(doc)).toBeUndefined();
  });

  it('reads the live document by default', () => {
    const block = document.createElement('script');
    block.type = 'application/json';
    block.id = BRANDING_ELEMENT_ID;
    block.textContent = '{"mode":"dark"}';
    document.head.append(block);

    try {
      expect(readBranding()).toEqual({ mode: 'dark' });
    } finally {
      block.remove();
    }
  });
});
