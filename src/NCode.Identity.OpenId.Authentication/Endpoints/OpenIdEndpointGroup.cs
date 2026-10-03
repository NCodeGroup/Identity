#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery;
using NCode.Identity.OpenId.Authentication.Endpoints.Introspection;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation;
using NCode.Identity.OpenId.Authentication.Endpoints.Token;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// The route group for the OpenID protocol endpoints. An empty prefix preserves each endpoint's absolute route (for
/// example <c>/oauth2/token</c>) while still grouping them so the shared pipeline filters apply only to OpenID
/// endpoints: the <see cref="OpenIdEnvironmentEndpointFilter"/> that materializes the request environment (and opens
/// the resolved tenant's ambient scope) and the <see cref="OpenIdExceptionEndpointFilter"/> that renders exceptions as
/// standard OpenID error responses. The group declares the protocol endpoints it contains; their services are
/// registered by each endpoint's own registration.
/// </summary>
internal sealed class OpenIdEndpointGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the OpenID protocol group.
    /// </summary>
    public const string GroupName = "openid";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public string Prefix => string.Empty;

    /// <inheritdoc />
    public void Configure(RouteGroupBuilder group)
    {
        // The environment filter is outermost so the ambient tenant scope it opens is still active while the exception
        // filter renders an error.
        group.AddEndpointFilter<RouteGroupBuilder, OpenIdEnvironmentEndpointFilter>();
        group.AddEndpointFilter<RouteGroupBuilder, OpenIdExceptionEndpointFilter>();
    }

    /// <inheritdoc />
    public void Build(IEndpointGroupBuilder builder)
    {
        builder.AddEndpoint<DefaultAuthorizationEndpointHandler>();
        builder.AddEndpoint<DefaultContinueEndpointHandler>();
        builder.AddEndpoint<DefaultDiscoveryEndpointHandler>();
        builder.AddEndpoint<DefaultJwksEndpointHandler>();
        builder.AddEndpoint<DefaultTokenEndpointProvider>();
        builder.AddEndpoint<DefaultRevocationEndpointProvider>();
        builder.AddEndpoint<DefaultIntrospectionEndpointProvider>();
        builder.AddEndpoint<DefaultUserInfoEndpointProvider>();
    }
}
