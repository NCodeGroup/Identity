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

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the source expressions that a Content-Security-Policy directive lists, and the helpers that build the
/// expressions that depend on a value.
/// </summary>
[PublicAPI]
public static class ContentSecurityPolicySources
{
    private const int NonceByteCount = 16;

    /// <summary>
    /// The <c>'none'</c> source, which allows nothing.
    /// </summary>
    public const string None = "'none'";

    /// <summary>
    /// The <c>'self'</c> source, which allows the page's own origin.
    /// </summary>
    public const string Self = "'self'";

    /// <summary>
    /// The <c>'strict-dynamic'</c> source, which lets a trusted script load the scripts it needs.
    /// </summary>
    public const string StrictDynamic = "'strict-dynamic'";

    /// <summary>
    /// Creates the <c>'nonce-...'</c> source that allows the elements which carry the nonce.
    /// </summary>
    /// <param name="nonce">The nonce value, which the page repeats on each element that the source allows.</param>
    /// <returns>The nonce source.</returns>
    public static string Nonce(string nonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(nonce);
        return $"'nonce-{nonce}'";
    }

    /// <summary>
    /// Creates the <c>'sha256-...'</c> source that allows exactly one inline script.
    /// </summary>
    /// <param name="script">The text between the <c>script</c> element's tags, which the browser hashes as UTF-8.</param>
    /// <returns>The hash source.</returns>
    public static string Sha256(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(script));
        return $"'sha256-{Convert.ToBase64String(hash)}'";
    }

    /// <summary>
    /// Creates a fresh, unguessable nonce for one response.
    /// </summary>
    /// <returns>The nonce as 22 unpadded Base64Url characters, which encode 16 random bytes.</returns>
    public static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[NonceByteCount];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Creates the source that allows the origin of an absolute URI.
    /// </summary>
    /// <param name="uri">The absolute URI whose origin the source allows.</param>
    /// <returns>
    /// The host source, with the port unless it is the scheme's default, for an <c>http</c> or <c>https</c> URI whose
    /// host is a DNS name or an IPv4 address; otherwise the scheme source, because a host source cannot express an IPv6
    /// literal or an opaque URI.
    /// </returns>
    /// <exception cref="ArgumentException">The URI is not absolute.</exception>
    public static string Origin(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("The URI must be absolute.", nameof(uri));
        }

        if (
            uri.Scheme is not ("http" or "https")
            || uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4)
        )
        {
            return $"{uri.Scheme}:";
        }

        var port = uri.IsDefaultPort
            ? string.Empty
            : $":{uri.Port.ToString(CultureInfo.InvariantCulture)}";
        return $"{uri.Scheme}://{uri.IdnHost}{port}";
    }
}
