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
using NCode.Identity.OpenId.Authentication.Subject;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents an OAuth 2.0 Token Exchange grant
/// (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>). When
/// <see cref="ActorAuthentication"/> is present the exchange is a <em>delegation</em> (the issued token carries an
/// <c>act</c> claim identifying the acting party); otherwise it is an <em>impersonation</em>.
/// </summary>
[PublicAPI]
public readonly record struct TokenExchangeGrant
{
    /// <summary>
    /// Gets the <see cref="SubjectAuthentication"/> resolved from the validated <c>subject_token</c>: the party on
    /// behalf of whom the exchange is being made.
    /// </summary>
    public required SubjectAuthentication SubjectAuthentication { get; init; }

    /// <summary>
    /// Gets the <see cref="SubjectAuthentication"/> resolved from the validated <c>actor_token</c>, or
    /// <see langword="null"/> when the exchange is an impersonation rather than a delegation.
    /// </summary>
    public SubjectAuthentication? ActorAuthentication { get; init; }

    /// <summary>
    /// Gets the RFC 8693 token type identifier the client requested for the issued token.
    /// </summary>
    public required string RequestedTokenType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the exchange is a delegation (an <c>actor_token</c> was presented and
    /// authorized).
    /// </summary>
    public bool IsDelegation => ActorAuthentication.HasValue;
}
