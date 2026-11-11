import { createContext, use, useMemo, type ReactNode } from 'react';
import { DEFAULT_LOCALE, messagesFor, resolveLocale } from './locales.js';
import type { Messages } from './messages.js';

/** The language a page renders in: what the host selected, what the bundle renders it as, and the catalog. */
export type LocaleContextValue = {
  /**
   * The language tag the host wrote into the page, which the forms carry back as `ui_locales` so a page the host
   * redirects to renders in the same language; empty when the page carries none.
   */
  readonly pageLanguage: string;
  /** The language tag of the catalog that renders the page (see `resolveLocale`), used to format dates too. */
  readonly locale: string;
  /** The catalog that renders the page. */
  readonly messages: Messages;
};

const LocaleContext = createContext<LocaleContextValue>({
  pageLanguage: '',
  locale: DEFAULT_LOCALE,
  messages: messagesFor(DEFAULT_LOCALE),
});

export type LocaleProviderProps = {
  /** The language tag the host wrote into the page's `html` element, or empty for the default language. */
  readonly pageLanguage: string;
  readonly children: ReactNode;
};

/** Provides the page's language to every surface beneath it. */
export function LocaleProvider({ pageLanguage, children }: LocaleProviderProps) {
  const value = useMemo<LocaleContextValue>(() => {
    const locale = resolveLocale(pageLanguage);
    return { pageLanguage, locale, messages: messagesFor(locale) };
  }, [pageLanguage]);
  return <LocaleContext value={value}>{children}</LocaleContext>;
}

/** The page's language; the default language outside a {@link LocaleProvider}. */
export function useLocale(): LocaleContextValue {
  return use(LocaleContext);
}

/** The catalog that renders the page; the default language's outside a {@link LocaleProvider}. */
export function useMessages(): Messages {
  return use(LocaleContext).messages;
}
