// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The route group for the interactive login/consent/account surface. Like the protocol group, its prefix is the tenant
/// route, so the surface is served beneath the tenant it signs in to (for example
/// <c>/{tenantId}/account/login</c> under the path strategy), and the shared
/// <see cref="OpenIdEnvironmentEndpointFilter"/> — which materializes the request environment and opens the resolved
/// tenant's ambient scope the credential verifier reads — resolves the same tenant the protocol endpoints do. Unlike the protocol group,
/// it adds no exception filter: the interactive endpoints render HTML pages and redirects, not OpenID error responses.
/// It mirrors the protocol endpoints' registration so the surface flows through the same
/// <c>MapEndpointGroups</c> discovery as every other endpoint instead of a bespoke mapping call.
/// </summary>
// See ADR-1165 and ADR-1193.
internal sealed class InteractiveAuthenticationEndpointGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the interactive surface group.
    /// </summary>
    public const string GroupName = "interactive-authentication";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<ITenantResolver>().GetTenantRoute();

    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group) =>
        group.AddEndpointFilter<RouteGroupBuilder, OpenIdEnvironmentEndpointFilter>();

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder) =>
        builder.AddEndpoint<InteractiveAuthenticationEndpointHandler>();
}
