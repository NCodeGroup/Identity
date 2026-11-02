// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the <c>private_use</c> redirect URI kind: a redirect URI whose scheme is neither <c>http</c> nor <c>https</c>
/// and is a reverse domain name.
/// </summary>
/// <remarks>
/// A native application registers a private-use URI scheme to receive the authorization response, and the scheme is a
/// reverse domain name that the application controls, such as <c>com.example.app</c> (RFC 8252 §7.1). A scheme without a
/// period, such as <c>javascript</c>, <c>data</c>, or <c>myapp</c>, is of no kind.
/// </remarks>
// See ADR-1203.
internal sealed class PrivateUseRedirectUriKind : IRedirectUriKind
{
    /// <inheritdoc />
    public string KindName => OpenIdConstants.RedirectUriKinds.PrivateUse;

    /// <inheritdoc />
    public bool Matches(Uri redirectUri) =>
        !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        && !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && IsReverseDomainName(redirectUri.Scheme);

    /// <summary>
    /// Determines whether a URI scheme is a reverse domain name: at least two labels separated by a period, none of which
    /// is empty.
    /// </summary>
    /// <param name="scheme">The URI scheme to check.</param>
    /// <returns><c>true</c> when the scheme is a reverse domain name; otherwise, <c>false</c>.</returns>
    private static bool IsReverseDomainName(string scheme)
    {
        var labels = scheme.Split('.');
        return labels.Length >= 2 && labels.All(label => label.Length > 0);
    }
}
