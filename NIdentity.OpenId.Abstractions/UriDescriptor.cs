#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId;

// Uri is not a good choice for OpenID, because it does not support unicode host names.
// According to the spec, the host name should be in unicode, not punycode.
// https://github.com/IdentityServer/IdentityServer4/issues/2080
// https://nat.sakimura.org/2018/04/04/what-encoding-should-a-uri-in-openid-and-oauth-discovery-document-use-for-an-internationalized-domain-name-idn/#easy-footnote-1-1403

/// <summary>
/// Represents the components of an Uniform Resource Identifier (URI).
/// </summary>
public readonly struct UriDescriptor
{
    /// <summary>
    /// Gets or sets the scheme (e.g. "http" or "https") component of the URI.
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// Gets or sets the host component of the URI.
    /// The value should be unicode rather than punycode, and may have a port.
    /// IPv4 and IPv6 addresses are also allowed, and also may have ports.
    /// Use <c>IdnMapping.GetUnicode(value)</c> if you have a punycode host value.
    /// </summary>
    public required HostString Host { get; init; }

    /// <summary>
    /// Gets or sets the path component of the URI.
    /// This value must be in unescaped format. Use <c>PathString.FromUriComponent(value)</c>
    /// if you have a path value which is in an escaped format.
    /// </summary>
    public required PathString Path { get; init; }

    /// <summary>
    /// Gets or sets the query component of the URI.
    /// This value must be in escaped and delimited format with a leading '?' character.
    /// </summary>
    public QueryString Query { get; init; }

    /// <summary>
    /// Gets a canonical string representation of the URI. The format is "scheme://host[:port]/path[?query]" where
    /// the host is the unicode representation (not punycode), port is optional, path is unescaped, and query is escaped and optional.
    /// </summary>
    public override string ToString() => $"{Scheme}{Uri.SchemeDelimiter}{Host.Value}{Path.Value}{Query.Value}";
}
