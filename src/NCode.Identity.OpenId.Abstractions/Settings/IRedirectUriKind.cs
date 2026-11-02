// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Defines a named kind of redirect URI that the <c>redirect_uri_kinds_supported</c> setting lists.
/// </summary>
/// <remarks>
/// Examples of a kind are <c>https</c> and <c>loopback</c>. The vocabulary of kinds is open: a package adds a kind by
/// registering another implementation, and <see cref="IRedirectUriKindCatalog"/> resolves the registered kinds by name.
/// </remarks>
[PublicAPI]
public interface IRedirectUriKind
{
    /// <summary>
    /// Gets the name by which <c>redirect_uri_kinds_supported</c> lists this kind, for example <c>https</c>.
    /// </summary>
    string KindName { get; }

    /// <summary>
    /// Determines whether the specified redirect URI is of this kind.
    /// </summary>
    /// <param name="redirectUri">The redirect URI, parsed from the absolute URI that the client wrote.</param>
    /// <returns><c>true</c> if the redirect URI is of this kind; otherwise, <c>false</c>.</returns>
    bool Matches(Uri redirectUri);
}
