// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Describes one application the end-user has granted standing consent to, for the authorized-applications page.
/// </summary>
/// <param name="ClientId">The identifier of the client.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured.</param>
/// <param name="Scopes">The scopes the subject has granted to the client, with friendly descriptions when available.</param>
internal sealed record AuthorizedApplicationResponse(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScopeResponse> Scopes
);

/// <summary>
/// The JSON payload the authorized-applications page fetches to render the end-user's authorized applications.
/// </summary>
/// <param name="Applications">The applications the end-user has granted standing consent to.</param>
internal sealed record AuthorizedApplicationsResponse(
    IReadOnlyList<AuthorizedApplicationResponse> Applications
);
