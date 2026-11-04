import type { Messages } from './messages.js';

/** German. */
export const de: Messages = {
  login: {
    title: 'Anmelden',
    titleFor: (productName) => `Bei ${productName} anmelden`,
    username: 'Benutzername',
    password: 'Passwort',
    submit: 'Anmelden',
  },
  loginErrors: {
    invalidCredentials: 'Der Benutzername oder das Passwort ist falsch.',
    invalidRequest: 'Geben Sie sowohl einen Benutzernamen als auch ein Passwort ein.',
    accessDenied: 'Dieses Konto kann sich nicht anmelden. Wenden Sie sich an Ihren Administrator.',
    fallback: 'Die Anmeldung ist fehlgeschlagen. Bitte versuchen Sie es erneut.',
  },
  consent: {
    title: 'Zugriff autorisieren',
    anApplication: 'Eine Anwendung',
    prompt: (appName) =>
      `${appName} fordert Zugriff auf Ihr Konto an. Wählen Sie, was Sie erlauben möchten:`,
    deny: 'Ablehnen',
    allow: 'Erlauben',
  },
  logoutConfirm: {
    title: 'Abmelden',
    question: 'Möchten Sie sich abmelden?',
    questionFor: (productName) => `Möchten Sie sich von ${productName} abmelden?`,
    stay: 'Angemeldet bleiben',
    signOut: 'Abmelden',
  },
  authorizationError: {
    title: 'Autorisierungsfehler',
  },
  account: {
    title: 'Ihr Konto',
    titleFor: (productName) => `${productName}-Konto`,
    signedInAs: 'Angemeldet als',
    subject: 'Subjekt',
    signedInAt: 'Angemeldet am',
    methods: 'Methoden',
    identityProvider: 'Identitätsanbieter',
    session: 'Sitzung',
    loading: 'Ihre Sitzung wird geladen…',
    signOut: 'Abmelden',
    manageSessions: 'Aktive Sitzungen verwalten',
    changePassword: 'Passwort ändern',
  },
  sessions: {
    title: 'Aktive Sitzungen',
    titleFor: (productName) => `${productName} — aktive Sitzungen`,
    loading: 'Ihre Sitzungen werden geladen…',
    empty: 'Sie haben keine weiteren aktiven Sitzungen.',
    unknownDevice: 'Unbekanntes Gerät',
    thisDevice: 'Dieses Gerät',
    signOut: 'Abmelden',
    ipAddress: 'IP-Adresse',
    lastActive: 'Zuletzt aktiv',
    signedIn: 'Angemeldet',
    signOutOthers: 'Überall sonst abmelden',
    signOutAll: 'Überall abmelden',
    back: 'Zurück zu Ihrem Konto',
  },
  applications: {
    title: 'Autorisierte Anwendungen',
    titleFor: (productName) => `${productName} — autorisierte Anwendungen`,
    loading: 'Ihre Anwendungen werden geladen…',
    empty: 'Sie haben keine Anwendungen autorisiert.',
    revoke: 'Widerrufen',
    back: 'Zurück zu Ihrem Konto',
  },
  password: {
    title: 'Passwort ändern',
    titleFor: (productName) => `${productName} — Passwort ändern`,
    changed: 'Ihr Passwort wurde geändert. Ihre anderen Sitzungen wurden abgemeldet.',
    current: 'Aktuelles Passwort',
    next: 'Neues Passwort',
    nextHelp:
      'Verwenden Sie eine lange Passphrase, die Sie nirgendwo sonst verwenden. Häufige und kompromittierte Passwörter werden abgelehnt.',
    confirm: 'Neues Passwort bestätigen',
    submit: 'Passwort ändern',
    back: 'Zurück zu Ihrem Konto',
  },
  passwordErrors: {
    invalidRequest: 'Geben Sie Ihr aktuelles Passwort und ein neues Passwort ein.',
    passwordMismatch: 'Die neuen Passwörter stimmen nicht überein.',
    invalidCurrentPassword: 'Das aktuelle Passwort ist falsch.',
    notSupported: 'Ihr Konto hat kein Passwort, das hier geändert werden kann.',
    passwordTooShort:
      'Das neue Passwort ist zu kurz. Eine Passphrase aus mehreren Wörtern ist lang genug und leicht zu merken.',
    passwordTooLong: 'Das neue Passwort ist zu lang. Wählen Sie ein kürzeres.',
    passwordBlocklisted:
      'Das neue Passwort ist zu häufig oder bekanntermaßen kompromittiert. Wählen Sie eines, das Sie nirgendwo sonst verwenden.',
    fallback: 'Das Passwort konnte nicht geändert werden. Bitte versuchen Sie es erneut.',
  },
  environmentBanner: (label) => `${label}-Umgebung — nicht für den Produktionseinsatz`,
};
