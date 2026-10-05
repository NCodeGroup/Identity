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

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Defines the well-known <see cref="IAuditEvent.Action"/> values emitted by the authentication
/// subsystem's audit events, so consumers can subscribe to or filter on them without magic strings.
/// </summary>
[PublicAPI]
public static class OpenIdAuditActions
{
    /// <summary>
    /// A security token was issued.
    /// </summary>
    public const string TokenIssued = "token.issued";

    /// <summary>
    /// A token (a refresh-token grant) was revoked.
    /// </summary>
    public const string TokenRevoked = "token.revoked";

    /// <summary>
    /// A refresh token was presented whose grant had already been revoked (a reuse/replay signal).
    /// </summary>
    public const string RefreshTokenReplay = "token.refresh.replay";

    /// <summary>
    /// An authorization request was granted for a subject.
    /// </summary>
    public const string AuthorizationGranted = "authorization.granted";

    /// <summary>
    /// An authorization request was denied for a subject.
    /// </summary>
    public const string AuthorizationDenied = "authorization.denied";

    /// <summary>
    /// Authenticating a client at the token endpoint failed.
    /// </summary>
    public const string ClientAuthentication = "client.authentication";

    /// <summary>
    /// A protocol endpoint returned an <c>OAuth</c> or <c>OpenID Connect</c> error response.
    /// </summary>
    public const string Error = "openid.error";
}
