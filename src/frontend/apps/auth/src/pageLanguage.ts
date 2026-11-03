/**
 * The value the bundle's `index.html` carries in the `lang` attribute of its `html` element. The host writes the
 * language tag of the locale it selected for the page in its place (`InteractiveBundleShell.LanguagePlaceholder` in
 * the .NET host must stay equal), so a page that still carries it was served without the host's selection.
 */
export const LANGUAGE_PLACEHOLDER = '__NCODE_LANG__';

/**
 * Reads the language the host selected for the page from the document's `lang` attribute: the first of the request's
 * `ui_locales` that the tenant supports and the surface renders, otherwise the tenant's default language. A page
 * without one, or one the host did not fill in, yields an empty string, and the bundle renders in its default language
 * (ADR-1402).
 */
export function readPageLanguage(doc: Document = document): string {
  const lang = doc.documentElement.lang.trim();
  return lang === LANGUAGE_PLACEHOLDER ? '' : lang;
}
