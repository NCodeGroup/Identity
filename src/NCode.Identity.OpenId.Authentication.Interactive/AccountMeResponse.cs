// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The JSON payload the account ("me") page fetches to render the current end-user's session.
/// </summary>
/// <param name="Subject">The subject identifier (<c>sub</c>) of the signed-in end-user.</param>
/// <param name="Name">A human-readable display name (the <c>name</c> or <c>preferred_username</c> claim), or the
/// subject identifier when neither is present.</param>
/// <param name="AuthTime">The time of authentication as a Unix timestamp in seconds, or <see langword="null"/> when
/// unknown.</param>
/// <param name="Amr">The authentication method references (<c>amr</c>) of the session.</param>
/// <param name="Idp">The identity provider (<c>idp</c>) that authenticated the end-user, or <see langword="null"/>.</param>
/// <param name="SessionId">The opaque server-side session identifier (<c>sid</c>), or <see langword="null"/>.</param>
internal sealed record AccountMeResponse(
    string Subject,
    string? Name,
    long? AuthTime,
    IReadOnlyList<string> Amr,
    string? Idp,
    string? SessionId
);
