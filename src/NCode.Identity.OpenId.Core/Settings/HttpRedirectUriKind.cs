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
