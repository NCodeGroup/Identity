#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
// See ADR-0203.
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
