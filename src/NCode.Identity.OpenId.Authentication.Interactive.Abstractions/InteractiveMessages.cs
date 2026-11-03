// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The text of the pages the interactive surface renders on the server, in one locale: the no-JavaScript login,
/// consent, logout confirmation and authorization error pages, and the messages the login page shows for a sign-in
/// that fails. Every value is written HTML-encoded, so a message holds plain text.
/// </summary>
// See ADR-1402.
[PublicAPI]
public sealed record InteractiveMessages
{
    /// <summary>
    /// Gets the heading and the document title of the login page.
    /// </summary>
    public required string SignInTitle { get; init; }

    /// <summary>
    /// Gets the label of the username field.
    /// </summary>
    public required string UsernameLabel { get; init; }

    /// <summary>
    /// Gets the label of the password field.
    /// </summary>
    public required string PasswordLabel { get; init; }

    /// <summary>
    /// Gets the text of the button that submits the login form.
    /// </summary>
    public required string SignInButton { get; init; }

    /// <summary>
    /// Gets the heading and the document title of the consent page.
    /// </summary>
    public required string AuthorizeAccessTitle { get; init; }

    /// <summary>
    /// Gets the sentence that introduces the requested scopes on the consent page, in which the <c>{0}</c> placeholder
    /// stands for the name of the application that requests access.
    /// </summary>
    public required string ConsentPromptFormat { get; init; }

    /// <summary>
    /// Gets the text of the button that denies the consent.
    /// </summary>
    public required string DenyButton { get; init; }

    /// <summary>
    /// Gets the text of the button that grants the consent.
    /// </summary>
    public required string AllowButton { get; init; }

    /// <summary>
    /// Gets the heading and the document title of the logout confirmation page.
    /// </summary>
    public required string SignOutTitle { get; init; }

    /// <summary>
    /// Gets the question the logout confirmation page asks.
    /// </summary>
    public required string SignOutQuestion { get; init; }

    /// <summary>
    /// Gets the text of the button that declines the logout.
    /// </summary>
    public required string StaySignedInButton { get; init; }

    /// <summary>
    /// Gets the text of the button that confirms the logout.
    /// </summary>
    public required string SignOutButton { get; init; }

    /// <summary>
    /// Gets the heading and the document title of the authorization error page.
    /// </summary>
    public required string AuthorizationErrorTitle { get; init; }

    /// <summary>
    /// Gets the message the login page shows when the username or the password is incorrect.
    /// </summary>
    public required string InvalidCredentialsError { get; init; }

    /// <summary>
    /// Gets the message the login page shows when the username or the password is missing.
    /// </summary>
    public required string MissingCredentialsError { get; init; }

    /// <summary>
    /// Gets the message the login page shows when the account is refused a sign-in.
    /// </summary>
    public required string AccessDeniedError { get; init; }

    /// <summary>
    /// Gets the message the login page shows for any other sign-in failure.
    /// </summary>
    public required string SignInFailedError { get; init; }
}
