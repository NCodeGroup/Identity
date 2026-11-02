// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes the pending authorization request an interactive consent surface must present to the end-user: the client
/// requesting access (with its friendly display name) and the scopes it is requesting (with friendly descriptions and
/// which ones the subject has already granted, so previously-consented scopes can be pre-selected). It is resolved
/// (without consuming) from the continuation that the authorize endpoint parked when it redirected to the consent page.
/// </summary>
/// <param name="ClientId">The natural identifier of the client requesting access.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured.</param>
/// <param name="Scopes">The scopes the client is requesting, each with a friendly description (when available) and
/// whether the subject has already granted it.</param>
[PublicAPI]
public sealed record PendingConsent(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScope> Scopes
);
