// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Resolves the registered redirect URI kinds by name and judges a redirect URI against a client's settings.
/// </summary>
/// <remarks>
/// The registered kinds are the <see cref="IRedirectUriKind"/> implementations known to the OP.
/// </remarks>
[PublicAPI]
public interface IRedirectUriKindCatalog
{
    /// <summary>
    /// Determines whether <c>redirect_uri_kinds_supported</c> permits the specified redirect URI.
    /// </summary>
    /// <remarks>
    /// A redirect URI is permitted when it is of a registered kind that <c>redirect_uri_kinds_supported</c> lists, or of
    /// any registered kind while the setting is unset. A value that is not an absolute URI, or a URI of no registered
    /// kind, is never permitted.
    /// </remarks>
    /// <param name="settings">The effective settings of the scope to judge the redirect URI against.</param>
    /// <param name="redirectUri">The redirect URI as the client wrote it.</param>
    /// <returns><c>true</c> if the redirect URI is permitted; otherwise, <c>false</c>.</returns>
    bool IsPermitted(IReadOnlySettingCollection settings, string redirectUri);
}
