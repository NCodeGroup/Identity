/** The non-redirectable authorization error the host rendered at the protocol endpoint. */
export type ServerAuthorizationError = {
  /** The error code, from the element's `data-error-code` attribute or else its text. */
  readonly code: string;
  /** The human-readable description the host showed, or `null` when it gave none. */
  readonly description: string | null;
};

/**
 * The state of the page the host rendered. The page the host rendered is the bundle's initial state, so mounting
 * discards no input: what the end-user typed or the browser autofilled, the field that had focus, the consent scopes
 * the end-user cleared, and the authorization error the host reported (ADR-0253).
 */
export type ServerPage = {
  /** The non-empty values of the text, email and password inputs, by input id. */
  readonly fieldValues: Readonly<Record<string, string>>;
  /** The values of the consent `scope` checkboxes the end-user cleared. */
  readonly clearedScopes: readonly string[];
  /** The id of the input that had focus, or `null` when none inside the page had. */
  readonly focusedFieldId: string | null;
  /** The authorization error the page reports, or `null` when it reports none. */
  readonly authorizationError: ServerAuthorizationError | null;
};

/** The state of a page that has no input and reports no error. */
export const EMPTY_SERVER_PAGE: ServerPage = {
  fieldValues: {},
  clearedScopes: [],
  focusedFieldId: null,
  authorizationError: null,
};

const CARRIED_INPUT_TYPES: ReadonlySet<string> = new Set(['text', 'email', 'password']);

/**
 * Reads the state of the page the host rendered into the given container, before the bundle replaces it. The active
 * element is passed in so the caller decides which document's focus is read.
 */
export function readServerPage(container: HTMLElement, activeElement: Element | null): ServerPage {
  const fieldValues: Record<string, string> = {};
  for (const input of container.querySelectorAll<HTMLInputElement>('input[id]')) {
    if (CARRIED_INPUT_TYPES.has(input.type) && input.value !== '') {
      fieldValues[input.id] = input.value;
    }
  }

  const clearedScopes = Array.from(
    container.querySelectorAll<HTMLInputElement>('input[type="checkbox"][name="scope"]'),
  )
    .filter((checkbox) => !checkbox.checked)
    .map((checkbox) => checkbox.value);

  const focusedFieldId =
    activeElement instanceof HTMLInputElement &&
    activeElement.id !== '' &&
    container.contains(activeElement)
      ? activeElement.id
      : null;

  const errorElement = container.querySelector('#authorization-error');
  const authorizationError: ServerAuthorizationError | null = errorElement
    ? {
        // An element's textContent is never null.
        code: errorElement.getAttribute('data-error-code') ?? errorElement.textContent!,
        description:
          container.querySelector('#authorization-error-description')?.textContent ?? null,
      }
    : null;

  return { fieldValues, clearedScopes, focusedFieldId, authorizationError };
}

/**
 * Writes a value carried from the page the host rendered into an input, only when the input is empty. It writes the
 * DOM `value` property, never the `value` attribute that React's `defaultValue` writes, so a carried password stays
 * out of the markup.
 */
export function writeCarriedValue(input: HTMLInputElement | null, value: string | undefined): void {
  if (input !== null && value !== undefined && input.value === '') {
    input.value = value;
  }
}
