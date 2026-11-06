// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The paths of the endpoints the <c>Backend for Frontend (BFF)</c> maps for each frontend.
/// </summary>
/// <remarks>
/// Every path is relative to the path prefix of the frontend, which is <see cref="BffDefaults.EndpointPrefix"/> by
/// default.
/// </remarks>
[PublicAPI]
public static class BffEndpointPaths
{
    /// <summary>
    /// The path of the endpoint that starts the login of a user.
    /// </summary>
    public const string Login = "/login";

    /// <summary>
    /// The path of the endpoint that completes the login of a user after the identity provider redirects back.
    /// </summary>
    public const string Callback = "/callback";

    /// <summary>
    /// The path of the endpoint that answers the document of the signed-in user.
    /// </summary>
    public const string User = "/user";

    /// <summary>
    /// The path of the endpoint that signs the user out.
    /// </summary>
    public const string Logout = "/logout";

    /// <summary>
    /// The path of the endpoint that receives the back-channel logout requests of the identity provider.
    /// </summary>
    public const string BackChannelLogout = "/backchannel-logout";

    /// <summary>
    /// The path of the endpoint that publishes the JSON Web Key Set of the frontend.
    /// </summary>
    public const string Jwks = "/jwks";
}
