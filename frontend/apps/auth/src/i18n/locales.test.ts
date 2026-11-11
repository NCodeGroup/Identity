import { describe, expect, it } from 'vitest';
import { de } from './de.js';
import { en } from './en.js';
import { es } from './es.js';
import { fr } from './fr.js';
import { DEFAULT_LOCALE, RENDERABLE_LOCALES, messagesFor, resolveLocale } from './locales.js';
import type { Messages } from './messages.js';

describe('resolveLocale', () => {
  it('resolves a renderable tag to its catalog, whatever its case', () => {
    expect(resolveLocale('fr')).toBe('fr');
    expect(resolveLocale('FR')).toBe('fr');
    expect(resolveLocale(' de ')).toBe('de');
  });

  it('resolves a tag with a region to the language it extends', () => {
    expect(resolveLocale('fr-CA')).toBe('fr');
    expect(resolveLocale('es-419')).toBe('es');
    expect(resolveLocale('de-Latn-DE')).toBe('de');
  });

  it('resolves an unrenderable, empty or missing tag to the default language', () => {
    expect(resolveLocale('zz')).toBe(DEFAULT_LOCALE);
    expect(resolveLocale('zz-ZZ')).toBe(DEFAULT_LOCALE);
    expect(resolveLocale('')).toBe(DEFAULT_LOCALE);
    expect(resolveLocale(null)).toBe(DEFAULT_LOCALE);
    expect(resolveLocale(undefined)).toBe(DEFAULT_LOCALE);
  });

  it('lists English first among the renderable locales', () => {
    expect(RENDERABLE_LOCALES).toContain(DEFAULT_LOCALE);
    expect(RENDERABLE_LOCALES).toEqual(['de', 'en', 'es', 'fr']);
  });
});

describe('messagesFor', () => {
  it('returns the catalog of a resolved locale and English for any other', () => {
    expect(messagesFor('fr')).toBe(fr);
    expect(messagesFor('de')).toBe(de);
    expect(messagesFor('es')).toBe(es);
    expect(messagesFor('en')).toBe(en);
    expect(messagesFor('zz')).toBe(en);
  });
});

describe('the catalogs', () => {
  function flatten(messages: Messages): Record<string, string | ((value: string) => string)> {
    const flat: Record<string, string | ((value: string) => string)> = {};
    for (const [group, value] of Object.entries(messages)) {
      if (typeof value === 'function') {
        flat[group] = value;
        continue;
      }
      for (const [key, text] of Object.entries(value)) {
        flat[`${group}.${key}`] = text as string | ((value: string) => string);
      }
    }
    return flat;
  }

  it.each([
    ['en', en],
    ['de', de],
    ['es', es],
    ['fr', fr],
  ])(
    '%s places the value in every sentence that takes one and leaves no text empty',
    (_, catalog) => {
      for (const [key, text] of Object.entries(flatten(catalog))) {
        if (typeof text === 'function') {
          expect(text('Acme'), key).toContain('Acme');
        } else {
          expect(text.trim(), key).not.toBe('');
        }
      }
    },
  );
});
