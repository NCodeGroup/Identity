// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Management.Endpoints.Caller;
using NCode.Identity.OpenId.Management.Endpoints.Capabilities;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Platform;
using NCode.Identity.OpenId.Management.Endpoints.Teams;
using NCode.Identity.OpenId.Management.Endpoints.Tenants;
using NCode.Identity.OpenId.Management.OpenApi;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The root management route group (<c>/api</c>). It materializes the OpenID request environment for every management
/// endpoint via <see cref="ManagementEnvironmentEndpointFilter"/> but opens no tenant scope, so control-plane
/// families (platform, teams, tenant provisioning) act on unscoped rows. It declares on every route the
/// <c>409</c> and <c>503</c> problems that its settings and root-key filters answer. It also describes the problem
/// responses that every route shares in the OpenAPI document, and answers a thrown request-binding failure with its
/// status.
/// Its endpoints have confidential bodies, so HTTP logging never writes their request or response bodies.
/// </summary>
internal sealed class ManagementRootGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the root management group.
    /// </summary>
    public const string GroupName = "api";

    /// <summary>
    /// The path every management route starts with.
    /// </summary>
    internal const string RoutePrefix = "/api";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse(RoutePrefix);

    // See ADR-1480.
    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group)
    {
        // Every management endpoint's body can carry a credential or personal data, so HTTP logging never writes one (ADR-1503).
        group
            .AddEndpointFilter<RouteGroupBuilder, ManagementEnvironmentEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, ManagementAuthenticationEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, StoreConcurrencyEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, UnreadableSettingsEndpointFilter>()
            .AddEndpointFilter<RouteGroupBuilder, OwnerRootKeyUnavailableEndpointFilter>()
            .WithConfidentialBodies()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .AddOpenApiOperationTransformer(
                new ManagementProblemResponseOperationTransformer().TransformAsync
            );

        ((IEndpointConventionBuilder)group).Add(BindingExceptionEndpointConvention.Apply);
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
