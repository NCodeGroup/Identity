// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents endpoint metadata stating whether an endpoint accepts a client bound to a resource server. Such a client
/// is the resource server's credential for the calls the resource server makes to the authorization server, and an
/// endpoint refuses it unless the endpoint carries this metadata with <see cref="AcceptsResourceServerClients"/> set
/// to <c>true</c>: a client bound to a resource server is not resolved anywhere else.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointResourceServerClientMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint accepts a client bound to a resource server.
    /// </summary>
    bool AcceptsResourceServerClients { get; }
}
