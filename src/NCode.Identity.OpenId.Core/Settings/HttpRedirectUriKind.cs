// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <c>http</c> redirect URI kind: an <c>http</c> redirect URI that is not of the <c>loopback</c> kind.
/// </summary>
/// <remarks>
/// The name <c>localhost</c> is not a loopback IP literal (RFC 8252 §8.3), so a redirect URI that uses it is of this
/// kind. An <c>http</c> redirect URI whose host is a loopback IP literal is of the <c>loopback</c> kind instead
/// (RFC 8252 §7.3).
/// </remarks>
internal sealed class HttpRedirectUriKind : IRedirectUriKind
{
    /// <inheritdoc />
    public string KindName => OpenIdConstants.RedirectUriKinds.Http;

    /// <inheritdoc />
    public bool Matches(Uri redirectUri) =>
        string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        && !LoopbackRedirectUriKind.IsLoopbackIp(redirectUri);
}
