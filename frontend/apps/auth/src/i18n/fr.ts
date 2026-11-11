import type { Messages } from './messages.js';

/** French. */
export const fr: Messages = {
  login: {
    title: 'Se connecter',
    titleFor: (productName) => `Se connecter à ${productName}`,
    username: 'Nom d’utilisateur',
    password: 'Mot de passe',
    submit: 'Se connecter',
  },
  loginErrors: {
    invalidCredentials: 'Le nom d’utilisateur ou le mot de passe est incorrect.',
    invalidRequest: 'Saisissez un nom d’utilisateur et un mot de passe.',
    accessDenied: 'Ce compte ne peut pas se connecter. Contactez votre administrateur.',
    fallback: 'La connexion a échoué. Veuillez réessayer.',
  },
  consent: {
    title: 'Autoriser l’accès',
    anApplication: 'Une application',
    prompt: (appName) =>
      `${appName} demande l’accès à votre compte. Choisissez ce que vous autorisez :`,
    deny: 'Refuser',
    allow: 'Autoriser',
  },
  logoutConfirm: {
    title: 'Se déconnecter',
    question: 'Voulez-vous vous déconnecter ?',
    questionFor: (productName) => `Voulez-vous vous déconnecter de ${productName} ?`,
    stay: 'Rester connecté',
    signOut: 'Se déconnecter',
  },
  authorizationError: {
    title: 'Erreur d’autorisation',
  },
  account: {
    title: 'Votre compte',
    titleFor: (productName) => `Compte ${productName}`,
    signedInAs: 'Connecté en tant que',
    subject: 'Sujet',
    signedInAt: 'Connecté le',
    methods: 'Méthodes',
    identityProvider: 'Fournisseur d’identité',
    session: 'Session',
    loading: 'Chargement de votre session…',
    signOut: 'Se déconnecter',
    manageSessions: 'Gérer les sessions actives',
    changePassword: 'Changer le mot de passe',
  },
  sessions: {
    title: 'Sessions actives',
    titleFor: (productName) => `${productName} — sessions actives`,
    loading: 'Chargement de vos sessions…',
    empty: 'Vous n’avez aucune autre session active.',
    unknownDevice: 'Appareil inconnu',
    thisDevice: 'Cet appareil',
    signOut: 'Déconnecter',
    ipAddress: 'Adresse IP',
    lastActive: 'Dernière activité',
    signedIn: 'Connexion',
    signOutOthers: 'Se déconnecter partout ailleurs',
    signOutAll: 'Se déconnecter partout',
    back: 'Retour à votre compte',
  },
  applications: {
    title: 'Applications autorisées',
    titleFor: (productName) => `${productName} — applications autorisées`,
    loading: 'Chargement de vos applications…',
    empty: 'Vous n’avez autorisé aucune application.',
    revoke: 'Révoquer',
    back: 'Retour à votre compte',
  },
  password: {
    title: 'Changer le mot de passe',
    titleFor: (productName) => `${productName} — changer le mot de passe`,
    changed: 'Votre mot de passe a été changé. Vos autres sessions ont été déconnectées.',
    current: 'Mot de passe actuel',
    next: 'Nouveau mot de passe',
    nextHelp:
      'Utilisez une phrase de passe longue que vous n’utilisez nulle part ailleurs. Les mots de passe courants ou compromis sont refusés.',
    confirm: 'Confirmer le nouveau mot de passe',
    submit: 'Changer le mot de passe',
    back: 'Retour à votre compte',
  },
  passwordErrors: {
    invalidRequest: 'Saisissez votre mot de passe actuel et un nouveau mot de passe.',
    passwordMismatch: 'Les nouveaux mots de passe ne correspondent pas.',
    invalidCurrentPassword: 'Le mot de passe actuel est incorrect.',
    notSupported: 'Votre compte n’a pas de mot de passe à changer ici.',
    passwordTooShort:
      'Le nouveau mot de passe est trop court. Une phrase de passe de plusieurs mots est assez longue et facile à retenir.',
    passwordTooLong: 'Le nouveau mot de passe est trop long. Choisissez-en un plus court.',
    passwordBlocklisted:
      'Le nouveau mot de passe est trop courant ou connu comme compromis. Choisissez-en un que vous n’utilisez nulle part ailleurs.',
    fallback: 'Le mot de passe n’a pas pu être changé. Veuillez réessayer.',
  },
  environmentBanner: (label) => `Environnement ${label} — pas pour un usage en production`,
};
