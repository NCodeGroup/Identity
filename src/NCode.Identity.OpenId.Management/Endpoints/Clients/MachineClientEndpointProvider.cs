// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Maps the routes that create and list the machine clients of the platform and of a team, which the root group serves
/// because they name no tenant.
/// </summary>
// See ADR-1466.
internal sealed class MachineClientEndpointProvider(
    ClientApiEndpointHandler clientApiEndpointHandler
) : IEndpointProvider
{
    private ClientApiEndpointHandler ClientApiEndpointHandler { get; } = clientApiEndpointHandler;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var platformClients = endpoints
            .MapGroup("/platform/clients")
            .WithTags(OpenIdConstants.EndpointTags.Clients);

        platformClients
            .MapPost("", ClientApiEndpointHandler.CreatePlatformClientAsync)
            .Produces<ClientResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        platformClients
            .MapGet("", ClientApiEndpointHandler.ListPlatformClientsAsync)
            .Produces<CollectionResource<ClientResource>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var teamClients = endpoints
            .MapGroup("/teams/{teamId}/clients")
            .WithTags(OpenIdConstants.EndpointTags.Clients);

        teamClients
            .MapPost("", ClientApiEndpointHandler.CreateTeamClientAsync)
            .Produces<ClientResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        teamClients
            .MapGet("", ClientApiEndpointHandler.ListTeamClientsAsync)
            .Produces<CollectionResource<ClientResource>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
