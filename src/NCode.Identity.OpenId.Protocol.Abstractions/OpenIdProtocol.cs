// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the <c>OAuth 2.0</c> and <c>OpenID Connect</c> wire vocabulary that is shared by the OpenID provider and
/// the protocol client. Its constants name well-known members of open vocabularies without closing them.
/// </summary>
// See ADR-1150.
[PublicAPI]
public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains the space ' ' character which is used as the separator in string lists.
    /// </summary>
    public const char ParameterSeparatorChar = ' ';

    /// <summary>
    /// Contains the space ' ' character (as a string) which is used as the separator in string lists.
    /// </summary>
    public const string ParameterSeparatorString = " ";

    /// <summary>
    /// Contains the URN prefix for a <c>request_uri</c> value issued by the pushed authorization request endpoint
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc9126#section-2.2">RFC 9126</see>). The full value is this
    /// prefix followed by a high-entropy, single-use reference to the stored request.
    /// </summary>
    public const string PushedAuthorizationRequestUriPrefix = "urn:ietf:params:oauth:request_uri:";
}
