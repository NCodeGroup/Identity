// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.OpenId.Management.Endpoints.Caller;
using NCode.Identity.OpenId.Management.Endpoints.Capabilities;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Platform;
using NCode.Identity.OpenId.Management.Endpoints.Teams;
using NCode.Identity.OpenId.Management.Endpoints.Tenants;
using NCode.Identity.OpenId.Management.OpenApi;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The root management route group (<c>/api</c>). It materializes the OpenID request environment for every management
/// endpoint via <see cref="ManagementEnvironmentEndpointFilter"/> but opens no tenant scope, so control-plane
/// families (platform, teams, tenant provisioning) act on unscoped rows. It declares on every route the
/// <c>409</c> and <c>503</c> problems that its settings and root-key filters answer. It also describes the problem
/// responses that every route shares in the OpenAPI document, and answers every error that carries no body as a
/// problem document.
/// </summary>
internal sealed class ManagementRootGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the root management group.
    /// </summary>
    public const string GroupName = "api";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse("/api");

    // See ADR-1480.
    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group)
    {
        group
            .AddEndpointFilter<RouteGroupBuilder, ManagementEnvironmentEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, ManagementAuthenticationEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, StoreConcurrencyEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, UnreadableSettingsEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, OwnerRootKeyUnavailableEndpointFilter>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .AddOpenApiOperationTransformer(
                new ManagementProblemResponseOperationTransformer().TransformAsync
            );

        ((IEndpointConventionBuilder)group).Add(ProblemResponseEndpointConvention.Apply);
    }

    /// <inheritdoc />
    public void ConfigureServices(IEndpointGroupBuilder builder)
    {
        builder.AddCapabilitiesEndpoint();
        builder.AddCallerEndpoint();
        builder.AddPlatformEndpoint();
        builder.AddTeamEndpoint();
        builder.AddMachineClientEndpoint();
        builder.AddTenantEndpoint();
        builder.AddGroup<TenantManagementGroup>();
    }
}
