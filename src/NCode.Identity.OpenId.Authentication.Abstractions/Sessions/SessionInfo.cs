// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Describes a single active end-user session when enumerating the sessions a subject holds (for example, the
/// "active sessions" management/account surface). It couples the session's opaque <c>sid</c> and server-side lifecycle
/// timestamps with the authentication context captured at sign-in.
/// </summary>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>).</param>
/// <param name="SubjectId">The stable, server-owned principal identifier the session belongs to.</param>
/// <param name="CreatedWhen">The time the session was established.</param>
/// <param name="ExpiresWhen">The time the session ends unless it is used again: the end of its idle window or, if
/// earlier, its absolute end.</param>
/// <param name="Grant">The authentication context and relying-party list captured for the session.</param>
[PublicAPI]
public readonly record struct SessionInfo(
    string SessionId,
    string? SubjectId,
    DateTimeOffset CreatedWhen,
    DateTimeOffset ExpiresWhen,
    SessionGrant Grant
);
