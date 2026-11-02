// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <c>https</c> redirect URI kind: a redirect URI whose scheme is <c>https</c>.
/// </summary>
/// <remarks>
/// Every <c>https</c> URI is of this kind, including one whose host is a loopback IP literal, because RFC 8252 §7.3 and
/// §8.3 concern <c>http</c> redirect URIs only.
/// </remarks>
internal sealed class HttpsRedirectUriKind : IRedirectUriKind
{
    /// <inheritdoc />
    public string KindName => OpenIdConstants.RedirectUriKinds.Https;

    /// <inheritdoc />
    public bool Matches(Uri redirectUri) =>
        string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
}
