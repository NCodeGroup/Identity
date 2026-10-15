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

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Contains the details used to create a new server-side end-user session: the subject the session belongs to plus the
/// authentication context (<c>auth_time</c>, <c>amr</c>, <c>acr</c>, <c>idp</c>) captured when the session was
/// established and later projected into the tokens issued under it.
/// </summary>
[PublicAPI]
public readonly record struct CreateSessionRequest
{
    /// <summary>
    /// Gets the subject the session belongs to: the stable, server-owned principal identifier resolved for the
    /// end-user (ADR-0035), not the raw upstream/local subject. The session, and every grant issued under it, key on
    /// this single value so the same user is referenced consistently regardless of how they authenticated.
    /// </summary>
    public required string SubjectId { get; init; }

    /// <summary>
    /// Gets the time when the end-user authenticated. It is the source of the <c>auth_time</c> claim, which is emitted
    /// as seconds since the Unix epoch at token-issuance time.
    /// </summary>
    public required DateTimeOffset AuthTime { get; init; }

    /// <summary>
    /// Gets the authentication method references (the <c>amr</c> claim values) used to establish the session, or
    /// <c>null</c> when none were recorded.
    /// </summary>
    public IReadOnlyList<string>? AuthenticationMethods { get; init; }

    /// <summary>
    /// Gets the authentication context class reference (the <c>acr</c> claim value) that the authentication satisfied,
    /// or <c>null</c> when no authentication context class applies.
    /// </summary>
    public string? AuthenticationContextClassReference { get; init; }

    /// <summary>
    /// Gets the identifier of the identity provider (the <c>idp</c> claim value) that authenticated the end-user, or
    /// <c>null</c> when the OP itself authenticated the end-user.
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    /// Gets the optional lifetime of the session. When <c>null</c>, the session does not expire on its own.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Gets the optional sliding inactivity window after which the session expires if it is not used. When <c>null</c>,
    /// no idle timeout applies and the session is bounded only by <see cref="Lifetime"/> (if any). The value is stamped
    /// onto the session so the idle policy is later enforced without re-reading host configuration.
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; }

    /// <summary>
    /// Gets the optional cap on the number of simultaneous sessions the subject may hold. When the subject is at or
    /// above the cap, the oldest sessions are revoked so that creating this session leaves at most this many active.
    /// When <c>null</c>, no concurrency limit is enforced.
    /// </summary>
    public int? MaxConcurrentSessions { get; init; }
}
