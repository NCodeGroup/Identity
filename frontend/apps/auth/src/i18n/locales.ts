import { de } from './de.js';
import { en } from './en.js';
import { es } from './es.js';
import { fr } from './fr.js';
import type { Messages } from './messages.js';

/** The language tag of the bundle's default language, which every page renders in when no catalog matches. */
export const DEFAULT_LOCALE = 'en';

/**
 * The catalogs the bundle renders, by BCP 47 language tag in lower case. Adding a language is one entry here and one
 * catalog file; the host selects the page's language from the locales it registers, and this map decides which of
 * those the bundle can draw itself.
 */
const CATALOGS: Readonly<Record<string, Messages>> = { de, en, es, fr };

/** The language tags of the catalogs the bundle renders, in the order they are listed. */
export const RENDERABLE_LOCALES: readonly string[] = Object.keys(CATALOGS);

/**
 * Resolves the language tag a page carries to the catalog that renders it: the tag itself, then the tag with its last
 * subtag removed (RFC 4647 lookup, so `fr-CA` renders as `fr`), compared without regard to case; the default language
 * when none matches, so an unrenderable tag never leaves a page without text.
 */
export function resolveLocale(languageTag: string | null | undefined): string {
  let candidate = (languageTag ?? '').trim().toLowerCase();
  while (candidate !== '') {
    if (Object.hasOwn(CATALOGS, candidate)) {
      return candidate;
    }
    const separator = candidate.lastIndexOf('-');
    candidate = separator < 0 ? '' : candidate.slice(0, separator);
  }
  return DEFAULT_LOCALE;
}

/** The catalog of a resolved locale (see {@link resolveLocale}). */
export function messagesFor(locale: string): Messages {
  return CATALOGS[locale] ?? en;
}
