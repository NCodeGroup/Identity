// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Describes a single scope on an interactive consent or authorized-applications surface.
/// </summary>
/// <param name="Value">The raw scope value.</param>
/// <param name="Description">A friendly description of the scope, or <see langword="null"/> when none is defined.</param>
/// <param name="Granted">Whether the subject has already granted this scope (so the consent surface can pre-select it).</param>
internal sealed record ConsentScopeResponse(string Value, string? Description, bool Granted);

/// <summary>
/// The JSON payload the consent page fetches to render the pending authorization request.
/// </summary>
/// <param name="ClientId">The identifier of the client requesting access.</param>
/// <param name="ClientName">The client's friendly display name, or <see langword="null"/> when none is configured.</param>
/// <param name="Scopes">The scopes the client is requesting, each with a friendly description and already-granted state.</param>
internal sealed record ConsentDetailsResponse(
    string ClientId,
    string? ClientName,
    IReadOnlyList<ConsentScopeResponse> Scopes
);
