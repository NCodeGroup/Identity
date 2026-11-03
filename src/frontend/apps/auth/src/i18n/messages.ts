/**
 * The text of every surface the bundle renders, in one locale. A catalog is a plain object typed by this shape, so a
 * locale that misses a message fails the type-check rather than rendering an empty string; a sentence that places a
 * value is a function, so each language orders its words itself (ADR-1402).
 */
export type Messages = {
  readonly login: {
    readonly title: string;
    readonly titleFor: (productName: string) => string;
    readonly username: string;
    readonly password: string;
    readonly submit: string;
  };
  /** The messages the login page shows for the `error` codes the host redirects back with. */
  readonly loginErrors: {
    readonly invalidCredentials: string;
    readonly invalidRequest: string;
    readonly accessDenied: string;
    readonly fallback: string;
  };
  readonly consent: {
    readonly title: string;
    readonly anApplication: string;
    readonly prompt: (appName: string) => string;
    readonly deny: string;
    readonly allow: string;
  };
  readonly logoutConfirm: {
    readonly title: string;
    readonly question: string;
    readonly questionFor: (productName: string) => string;
    readonly stay: string;
    readonly signOut: string;
  };
  readonly authorizationError: {
    readonly title: string;
  };
  readonly account: {
    readonly title: string;
    readonly titleFor: (productName: string) => string;
    readonly signedInAs: string;
    readonly subject: string;
    readonly signedInAt: string;
    readonly methods: string;
    readonly identityProvider: string;
    readonly session: string;
    readonly loading: string;
    readonly signOut: string;
    readonly manageSessions: string;
    readonly changePassword: string;
  };
  readonly sessions: {
    readonly title: string;
    readonly titleFor: (productName: string) => string;
    readonly loading: string;
    readonly empty: string;
    readonly unknownDevice: string;
    readonly thisDevice: string;
    readonly signOut: string;
    readonly ipAddress: string;
    readonly lastActive: string;
    readonly signedIn: string;
    readonly signOutOthers: string;
    readonly signOutAll: string;
    readonly back: string;
  };
  readonly applications: {
    readonly title: string;
    readonly titleFor: (productName: string) => string;
    readonly loading: string;
    readonly empty: string;
    readonly revoke: string;
    readonly back: string;
  };
  readonly password: {
    readonly title: string;
    readonly titleFor: (productName: string) => string;
    readonly changed: string;
    readonly current: string;
    readonly next: string;
    readonly nextHelp: string;
    readonly confirm: string;
    readonly submit: string;
    readonly back: string;
  };
  /** The messages the change-password page shows for the `error` codes the host redirects back with. */
  readonly passwordErrors: {
    readonly invalidRequest: string;
    readonly passwordMismatch: string;
    readonly invalidCurrentPassword: string;
    readonly notSupported: string;
    readonly passwordTooShort: string;
    readonly passwordTooLong: string;
    readonly passwordBlocklisted: string;
    readonly fallback: string;
  };
  /** The banner of a tenant that is not a production one, given the environment's label. */
  readonly environmentBanner: (label: string) => string;
};
