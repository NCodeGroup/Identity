// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Sessions;

/// <summary>
/// Represents an end-user session resource for the administrative active-sessions surface. This is a metadata-only
/// projection; no secret material is exposed.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SessionResource
{
    /// <summary>
    /// Gets the opaque session identifier (<c>sid</c>).
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the identifier of the subject the session belongs to, or <c>null</c> when not recorded.
    /// </summary>
    public required string? SubjectId { get; init; }

    /// <summary>
    /// Gets when the session was established.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the most recent time the session was observed in use.
    /// </summary>
    public required DateTimeOffset LastActivityWhen { get; init; }

    /// <summary>
    /// Gets when the session ends unless it is used again: the end of its idle window or, if earlier, the end of its
    /// absolute lifetime.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets when the end-user authenticated for the session (the source of the <c>auth_time</c> claim).
    /// </summary>
    public required DateTimeOffset AuthTime { get; init; }

    /// <summary>
    /// Gets the authentication method references (the <c>amr</c> claim values) captured at sign-in.
    /// </summary>
    public required IReadOnlyList<string> AuthenticationMethods { get; init; }

    /// <summary>
    /// Gets the authentication context class reference (the <c>acr</c> claim value), or <c>null</c> when none applies.
    /// </summary>
    public required string? AuthenticationContextClassReference { get; init; }

    /// <summary>
    /// Gets the identity provider (the <c>idp</c> claim value) that authenticated the end-user, or <c>null</c> when the
    /// OP itself authenticated the end-user.
    /// </summary>
    public required string? IdentityProvider { get; init; }

    /// <summary>
    /// Gets the client identifiers (relying parties) that have participated in the session.
    /// </summary>
    public required IReadOnlyList<string> ClientIds { get; init; }

    /// <summary>
    /// Gets the <c>User-Agent</c> observed when the session was established, or <c>null</c> when none was recorded.
    /// </summary>
    public required string? UserAgent { get; init; }

    /// <summary>
    /// Gets the remote IP address observed when the session was established, or <c>null</c> when none was recorded.
    /// </summary>
    public required string? RemoteIpAddress { get; init; }
}
