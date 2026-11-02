// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Results;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Commands;

/// <summary>
/// Represents a mediator command to populate a token introspection <see cref="Result"/> for an authenticated caller
/// (RFC 7662). Handlers determine whether the token is active for the caller and contribute the claims the caller may
/// see; register additional handlers to enrich the response. A token is disclosed only to the party that consumes it:
/// an access token to a caller acting for a resource server the token names in its audience, and a refresh token to
/// the client it was issued to.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="OpenIdClient">The authenticated <see cref="OpenIdClient"/> on whose behalf introspection is performed.</param>
/// <param name="ResourceServer">The resource server the caller acts for, or <c>null</c> when the caller is an ordinary
/// client or its resource server is not enabled.</param>
/// <param name="TokenIntrospectionRequest">The <see cref="ITokenIntrospectionRequest"/> identifying the token to introspect.</param>
/// <param name="Result">The mutable <see cref="IntrospectionResult"/> that handlers populate with the token's active state and claims.</param>
// See ADR-1029 and ADR-1168.
[PublicAPI]
public readonly record struct IntrospectTokenCommand(
    OpenIdContext OpenIdContext,
    OpenIdClient OpenIdClient,
    ResolvedResourceServer? ResourceServer,
    ITokenIntrospectionRequest TokenIntrospectionRequest,
    IntrospectionResult Result
) : ICommand;
