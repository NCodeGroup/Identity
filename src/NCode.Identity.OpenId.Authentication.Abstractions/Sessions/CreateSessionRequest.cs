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
/// Contains the details used to create a new end-user session.
/// </summary>
[PublicAPI]
public readonly record struct CreateSessionRequest
{
    /// <summary>
    /// Gets the identifier of the authenticated subject (end-user) that the session belongs to.
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
    /// Gets the identifier of the identity provider (the <c>idp</c> claim value) that authenticated the end-user, or
    /// <c>null</c> when the OP itself authenticated the end-user.
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    /// Gets the optional lifetime of the session. When <c>null</c>, the session does not expire on its own.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }
}
