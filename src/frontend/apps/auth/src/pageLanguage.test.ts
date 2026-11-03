import { afterEach, describe, expect, it } from 'vitest';
import { LANGUAGE_PLACEHOLDER, readPageLanguage } from './pageLanguage.js';

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
