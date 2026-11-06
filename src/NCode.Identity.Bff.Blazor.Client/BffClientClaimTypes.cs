// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Provides the claim types the backend-for-frontend (BFF) Blazor WebAssembly client adds to the user's principal.
/// Each value equals the server's <c>BffClaimTypes</c> value of the same name, which the WebAssembly client cannot
/// reference.
/// </summary>
[PublicAPI]
public static class BffClientClaimTypes
{
    /// <summary>
    /// The claim type that holds the URL a logout form posts to. It equals the server's <c>BffClaimTypes.LogoutUrl</c>.
    /// </summary>
    public const string LogoutUrl = "bff_logout_url";

    /// <summary>
    /// The claim type that holds the session id a logout form carries as its guard. It equals the server's
    /// <c>BffClaimTypes.LogoutSid</c>.
    /// </summary>
    public const string LogoutSid = "bff_logout_sid";
}
