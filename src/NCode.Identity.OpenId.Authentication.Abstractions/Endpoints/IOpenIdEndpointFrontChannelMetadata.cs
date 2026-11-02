// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents endpoint metadata marking a front-channel endpoint: one the user agent navigates to (the authorization,
/// continue, and end-session endpoints and the interactive login/consent surface) rather than one a client calls from
/// script. Such an endpoint never answers a cross-origin request with CORS headers (FAPI 2.0 Security Profile §5.3.2.1),
/// so the host applies no CORS policy to an endpoint that carries this metadata with <see cref="IsFrontChannel"/> set to
/// <c>true</c>. It is not CORS metadata, so a host that adds no CORS middleware serves the endpoint unchanged.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointFrontChannelMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint is a front-channel endpoint that no CORS policy applies to.
    /// </summary>
    bool IsFrontChannel { get; }
}
