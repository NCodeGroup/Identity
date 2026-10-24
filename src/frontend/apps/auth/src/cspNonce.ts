/** The selector of the `<meta>` element in which the host embeds the page's Content-Security-Policy nonce. */
export const CSP_NONCE_SELECTOR = 'meta[property="csp-nonce"]';

/**
 * Reads the nonce of the page's Content-Security-Policy. The host writes it into the `<meta property="csp-nonce">`
 * element, and a browser hides the attribute once the page is parsed, so the element's `nonce` property is read. A
 * page without the element, or with an empty nonce, resolves to `undefined` (ADR-0256).
 */
export function readCspNonce(doc: Document = document): string | undefined {
  const nonce = doc.querySelector<HTMLMetaElement>(CSP_NONCE_SELECTOR)?.nonce;
  return nonce ? nonce : undefined;
}
