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
