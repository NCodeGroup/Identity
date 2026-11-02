// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;

/// <summary>
/// Identifies the end-user whose session is being terminated by an RP-Initiated Logout, resolved from the
/// <c>id_token_hint</c> or the current OP login cookie.
/// </summary>
/// <param name="SubjectId">The identifier of the subject being logged out, or <see langword="null"/> when it could not
/// be resolved.</param>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>) being terminated, or <see langword="null"/> when
/// it could not be resolved.</param>
/// <param name="ClientId">The identifier of the relying party that requested the logout, or <see langword="null"/>.</param>
/// <param name="ParticipatingClientIds">The relying parties that participated in the session and are therefore
/// candidates for back-channel logout notification.</param>
[PublicAPI]
public readonly record struct EndSessionSubject(
    string? SubjectId,
    string? SessionId,
    string? ClientId,
    IReadOnlyList<string> ParticipatingClientIds
);
