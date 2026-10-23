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

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Represents the payload of a persisted end-user session (an OpenID Connect login at the OP). The session is persisted
/// keyed by its opaque <c>sid</c>, carries the authentication context captured at sign-in, and tracks the set of
/// relying parties (clients) the session has issued tokens to so they can be notified on back-channel logout.
/// </summary>
[PublicAPI]
public readonly record struct SessionGrant
{
    /// <summary>
    /// Gets the time when the end-user authenticated for this session. It is the source of the <c>auth_time</c> claim,
    /// which is emitted as seconds since the Unix epoch at token-issuance time.
    /// </summary>
    public required DateTimeOffset AuthTime { get; init; }

    /// <summary>
    /// Gets the authentication method references (the <c>amr</c> claim values) used to establish this session, or
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
    /// Gets the set of client identifiers (relying parties) that this session has issued tokens to. This is the
    /// notification list used for back-channel logout.
    /// </summary>
    public required IReadOnlyList<string> ClientIds { get; init; }

    /// <summary>
    /// Gets the most recent time the session was observed to be in use (an authorize request resolved it). It is the
    /// sliding base against which <see cref="IdleTimeout"/> is measured; a server-authoritative idle (inactivity)
    /// timeout expires the session once the elapsed time since this value exceeds <see cref="IdleTimeout"/>.
    /// </summary>
    public required DateTimeOffset LastActivityWhen { get; init; }

    /// <summary>
    /// Gets the sliding inactivity window after which the session expires if it is not used, or <c>null</c> when the
    /// session was stored without one, in which case only its absolute end bounds it. Stamped onto the session at
    /// creation so the idle policy travels with the session and is enforced without re-reading host configuration.
    /// </summary>
    public TimeSpan? IdleTimeout { get; init; }

    /// <summary>
    /// Gets the time the session ends however it is used, or <c>null</c> when the session was stored without one, in
    /// which case the expiry of its grant is its absolute end.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiresWhen { get; init; }

    /// <summary>
    /// Gets the <c>User-Agent</c> header observed when the session was established, used to describe the session's
    /// device/browser on an active-sessions surface, or <c>null</c> when none was recorded.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets the remote IP address observed when the session was established, used to describe where the session
    /// originated on an active-sessions surface, or <c>null</c> when none was recorded.
    /// </summary>
    public string? RemoteIpAddress { get; init; }

    /// <summary>
    /// Gets any unrecognized members read from the at-rest JSON, captured so that members written by a newer schema
    /// version are preserved rather than silently dropped when this session is read and rewritten by an older node
    /// during a mixed-version deployment.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
