// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Represents the parked RP-Initiated Logout request that waits for the end-user to confirm it, and the session the
/// request was asked for.
/// </summary>
/// <param name="IdTokenHint">The <c>id_token_hint</c> parameter of the request.</param>
/// <param name="ClientId">The <c>client_id</c> parameter of the request.</param>
/// <param name="PostLogoutRedirectUri">The <c>post_logout_redirect_uri</c> parameter of the request.</param>
/// <param name="State">The <c>state</c> parameter of the request.</param>
/// <param name="SessionId">The session (<c>sid</c>) the logout was asked for, or <see langword="null"/> when none.</param>
internal sealed record EndSessionContinuation(
    string? IdTokenHint,
    string? ClientId,
    string? PostLogoutRedirectUri,
    string? State,
    string? SessionId
);
