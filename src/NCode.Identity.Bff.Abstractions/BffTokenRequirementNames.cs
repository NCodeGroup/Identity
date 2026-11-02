// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known names of token requirements.
/// </summary>
/// <remarks>
/// The names are an open vocabulary: a package may register a requirement under a name that is not listed here. Names
/// are compared ordinally.
/// </remarks>
[PublicAPI]
public static class BffTokenRequirementNames
{
    /// <summary>
    /// The access token of the signed-in user.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// The BFF's own access token, from the <c>client_credentials</c> grant, for a request that has a session.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// The access token of the signed-in user when the request has a session; otherwise, the BFF's own access token.
    /// </summary>
    public const string UserOrClient = "user_or_client";

    /// <summary>
    /// An access token that the provider issues for the signed-in user's token through token exchange (RFC 8693),
    /// for a resource the user's own token is not issued for.
    /// </summary>
    public const string Exchange = "exchange";

    /// <summary>
    /// No access token: the request needs only a session.
    /// </summary>
    public const string None = "none";
}
