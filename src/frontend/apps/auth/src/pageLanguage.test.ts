import { afterEach, describe, expect, it } from 'vitest';
import indexHtml from '../index.html?raw';
import { LANGUAGE_PLACEHOLDER, readPageLanguage } from './pageLanguage.js';

describe('LANGUAGE_PLACEHOLDER', () => {
  it('is the lang attribute of the bundle index', () => {
    // The host writes the selected language in its place, so the index and the bundle must agree on it; the host's
    // own tests hold its side of the contract.
    expect(indexHtml).toContain(`<html lang="${LANGUAGE_PLACEHOLDER}">`);
  });
});

describe('readPageLanguage', () => {
  afterEach(() => {
    document.documentElement.removeAttribute('lang');
  });

  it('reads the language the host wrote into the document', () => {
    document.documentElement.lang = ' fr-CA ';

    expect(readPageLanguage()).toBe('fr-CA');
  });

  it('reads nothing from a document without a language', () => {
    expect(readPageLanguage()).toBe('');
  });

  it('reads nothing from a document the host did not fill in', () => {
    document.documentElement.lang = LANGUAGE_PLACEHOLDER;

    expect(readPageLanguage()).toBe('');
  });

  it('reads the given document', () => {
    const doc = document.implementation.createHTMLDocument();
    doc.documentElement.lang = 'de';

    expect(readPageLanguage(doc)).toBe('de');
  });
});
