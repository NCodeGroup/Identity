// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Describes a single end-user session termination: its cause, the subject and session scope, and the relying parties
/// that participated in the session (the back-channel logout notification list). A <see cref="SessionId"/> of
/// <see langword="null"/> denotes a subject-wide termination (for example, "sign out everywhere").
/// </summary>
/// <param name="Reason">Why the session is being terminated: one of <see cref="SessionTerminationReasons"/> or an
/// extension's own name.</param>
/// <param name="SubjectId">The stable, server-owned principal identifier whose session is terminated, when known.</param>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>) being terminated, or <see langword="null"/> for a
/// subject-wide termination.</param>
/// <param name="ParticipatingClientIds">The relying parties that participated in the terminated session(s) and are
/// therefore candidates for back-channel logout notification.</param>
[PublicAPI]
public readonly record struct SessionTermination(
    string Reason,
    string? SubjectId,
    string? SessionId,
    IReadOnlyList<string> ParticipatingClientIds
);
