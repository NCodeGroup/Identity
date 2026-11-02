// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <c>loopback</c> redirect URI kind: an <c>http</c> redirect URI whose host is a loopback IP literal.
/// </summary>
/// <remarks>
/// The loopback IP literals are <c>127.0.0.1</c> and <c>[::1]</c> (RFC 8252 §7.3). The name <c>localhost</c> is not a
/// loopback IP literal (RFC 8252 §8.3), so a redirect URI that uses it is of the <c>http</c> kind instead.
/// </remarks>
internal sealed class LoopbackRedirectUriKind : IRedirectUriKind
{
    /// <inheritdoc />
    public string KindName => OpenIdConstants.RedirectUriKinds.Loopback;

    /// <inheritdoc />
    public bool Matches(Uri redirectUri) => IsLoopbackIp(redirectUri);

    /// <summary>
    /// Determines whether the specified redirect URI is an <c>http</c> URI whose host is a loopback IP literal.
    /// </summary>
    /// <param name="redirectUri">The redirect URI, parsed from the absolute URI that the client wrote.</param>
    /// <returns><c>true</c> if the scheme is <c>http</c> and the host is a loopback IP literal; otherwise, <c>false</c>.</returns>
    public static bool IsLoopbackIp(Uri redirectUri) =>
        string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        && redirectUri.Host is "127.0.0.1" or "[::1]";
}
