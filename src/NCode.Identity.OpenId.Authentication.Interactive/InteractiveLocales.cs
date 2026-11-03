// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The locales the interactive surface ships, which <c>AddInteractiveAuthentication</c> registers. English is the
/// first registered locale, so it is the surface's default language for a tenant that sets no
/// <c>default_ui_locale</c>. A host that changes the wording of a page registers a locale built from one of these
/// with its own <see cref="InteractiveMessages"/>.
/// </summary>
// See ADR-1402.
[PublicAPI]
public static class InteractiveLocales
{
    /// <summary>
    /// Gets the English locale, <c>en</c>.
    /// </summary>
    public static InteractiveLocale English { get; } =
        new(
            "en",
            new InteractiveMessages
            {
                SignInTitle = "Sign in",
                UsernameLabel = "Username",
                PasswordLabel = "Password",
                SignInButton = "Sign in",
                AuthorizeAccessTitle = "Authorize access",
                ConsentPromptFormat =
                    "{0} is requesting access to your account. Choose what to allow:",
                DenyButton = "Deny",
                AllowButton = "Allow",
                SignOutTitle = "Sign out",
                SignOutQuestion = "Do you want to sign out?",
                StaySignedInButton = "Stay signed in",
                SignOutButton = "Sign out",
                AuthorizationErrorTitle = "Authorization error",
                InvalidCredentialsError = "The username or password is incorrect.",
                MissingCredentialsError = "Enter both a username and a password.",
                AccessDeniedError = "This account cannot sign in. Contact your administrator.",
                SignInFailedError = "Sign-in failed. Please try again.",
            }
        );

    /// <summary>
    /// Gets the German locale, <c>de</c>.
    /// </summary>
    public static InteractiveLocale German { get; } =
        new(
            "de",
            new InteractiveMessages
            {
                SignInTitle = "Anmelden",
                UsernameLabel = "Benutzername",
                PasswordLabel = "Passwort",
                SignInButton = "Anmelden",
                AuthorizeAccessTitle = "Zugriff autorisieren",
                ConsentPromptFormat =
                    "{0} fordert Zugriff auf Ihr Konto an. Wählen Sie, was Sie erlauben möchten:",
                DenyButton = "Ablehnen",
                AllowButton = "Erlauben",
                SignOutTitle = "Abmelden",
                SignOutQuestion = "Möchten Sie sich abmelden?",
                StaySignedInButton = "Angemeldet bleiben",
                SignOutButton = "Abmelden",
                AuthorizationErrorTitle = "Autorisierungsfehler",
                InvalidCredentialsError = "Der Benutzername oder das Passwort ist falsch.",
                MissingCredentialsError =
                    "Geben Sie sowohl einen Benutzernamen als auch ein Passwort ein.",
                AccessDeniedError =
                    "Dieses Konto kann sich nicht anmelden. Wenden Sie sich an Ihren Administrator.",
                SignInFailedError =
                    "Die Anmeldung ist fehlgeschlagen. Bitte versuchen Sie es erneut.",
            }
        );

    /// <summary>
    /// Gets the Spanish locale, <c>es</c>.
    /// </summary>
    public static InteractiveLocale Spanish { get; } =
        new(
            "es",
            new InteractiveMessages
            {
                SignInTitle = "Iniciar sesión",
                UsernameLabel = "Nombre de usuario",
                PasswordLabel = "Contraseña",
                SignInButton = "Iniciar sesión",
                AuthorizeAccessTitle = "Autorizar acceso",
                ConsentPromptFormat = "{0} solicita acceso a su cuenta. Elija qué permitir:",
                DenyButton = "Denegar",
                AllowButton = "Permitir",
                SignOutTitle = "Cerrar sesión",
                SignOutQuestion = "¿Desea cerrar sesión?",
                StaySignedInButton = "Seguir conectado",
                SignOutButton = "Cerrar sesión",
                AuthorizationErrorTitle = "Error de autorización",
                InvalidCredentialsError = "El nombre de usuario o la contraseña no son correctos.",
                MissingCredentialsError = "Introduzca un nombre de usuario y una contraseña.",
                AccessDeniedError =
                    "Esta cuenta no puede iniciar sesión. Póngase en contacto con su administrador.",
                SignInFailedError = "No se pudo iniciar sesión. Inténtelo de nuevo.",
            }
        );

    /// <summary>
    /// Gets the French locale, <c>fr</c>.
    /// </summary>
    public static InteractiveLocale French { get; } =
        new(
            "fr",
            new InteractiveMessages
            {
                SignInTitle = "Se connecter",
                UsernameLabel = "Nom d’utilisateur",
                PasswordLabel = "Mot de passe",
                SignInButton = "Se connecter",
                AuthorizeAccessTitle = "Autoriser l’accès",
                ConsentPromptFormat =
                    "{0} demande l’accès à votre compte. Choisissez ce que vous autorisez :",
                DenyButton = "Refuser",
                AllowButton = "Autoriser",
                SignOutTitle = "Se déconnecter",
                SignOutQuestion = "Voulez-vous vous déconnecter ?",
                StaySignedInButton = "Rester connecté",
                SignOutButton = "Se déconnecter",
                AuthorizationErrorTitle = "Erreur d’autorisation",
                InvalidCredentialsError = "Le nom d’utilisateur ou le mot de passe est incorrect.",
                MissingCredentialsError = "Saisissez un nom d’utilisateur et un mot de passe.",
                AccessDeniedError =
                    "Ce compte ne peut pas se connecter. Contactez votre administrateur.",
                SignInFailedError = "La connexion a échoué. Veuillez réessayer.",
            }
        );

    /// <summary>
    /// Gets every locale the surface ships, in the order it registers them: English first.
    /// </summary>
    public static ImmutableArray<InteractiveLocale> All { get; } =
    [English, German, Spanish, French];
}
