// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes one application a subject has granted standing consent to, for the self-service "authorized applications"
/// surface that lets an end-user review and revoke the access they have granted.
/// </summary>
/// <param name="ClientId">The natural identifier of the client the subject consented to.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured (the
/// surface falls back to the identifier).</param>
/// <param name="Scopes">The scopes the subject has granted to the client, with friendly descriptions when available.</param>
[PublicAPI]
public sealed record AuthorizedApplication(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScope> Scopes
);
