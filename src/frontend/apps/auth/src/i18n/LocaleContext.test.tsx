import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { LocaleProvider, useLocale, useMessages } from './LocaleContext.js';
import { en } from './en.js';
import { fr } from './fr.js';

function Probe() {
  const { pageLanguage, locale } = useLocale();
  const messages = useMessages();
  return (
    <output data-testid="probe">
      {pageLanguage}|{locale}|{messages.login.title}
    </output>
  );
}

describe('LocaleProvider', () => {
  it('renders the page language the host selected with its catalog', () => {
    const { getByTestId } = render(
      <LocaleProvider pageLanguage="fr-CA">
        <Probe />
      </LocaleProvider>,
    );

    expect(getByTestId('probe')).toHaveTextContent(`fr-CA|fr|${fr.login.title}`);
  });

  it('keeps the page language the host selected while rendering the default language for one it cannot draw', () => {
    const { getByTestId } = render(
      <LocaleProvider pageLanguage="it">
        <Probe />
      </LocaleProvider>,
    );

    expect(getByTestId('probe')).toHaveTextContent(`it|en|${en.login.title}`);
  });

  it('renders the default language outside a provider', () => {
    const { getByTestId } = render(<Probe />);

    expect(getByTestId('probe')).toHaveTextContent(`|en|${en.login.title}`);
  });
});
