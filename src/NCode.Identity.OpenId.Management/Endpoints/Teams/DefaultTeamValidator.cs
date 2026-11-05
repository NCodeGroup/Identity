// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Provides the default implementation of <see cref="ITeamValidator"/>. A team is created and deleted under the
/// platform node and updated under its own node, authorized in the control-plane tenant that serves the request. A
/// team's identifier follows the form of a tenant identifier, is never reused after the team is deleted, and the
/// platform's own team can be neither disabled nor deleted. Disabling or restoring a team is also authorized at the
/// platform node, so only the platform suspends and restores a team.
/// </summary>
// See ADR-1403
internal class DefaultTeamValidator(
    IAuthorizationService authorizationService,
    IOptions<TenantResolutionOptions> tenantResolutionOptions
) : DefaultResourceValidator(authorizationService), ITeamValidator
{
    private IOptions<TenantResolutionOptions> TenantResolutionOptions { get; } =
        tenantResolutionOptions;

    private TenantResolutionOptions Options => TenantResolutionOptions.Value;

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        PersistedTeam team,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            ResourceNode.For(
                controlPlaneTenantId,
                ResourceNodeTypes.Platform,
                OpenIdConstants.PlatformResourceId
            ),
            Operations.Create,
            "The caller is not authorized to create this team.",
            "Authentication is required to create this team."
        );
        if (error is not null)
        {
            return error;
        }

        if (!TenantIdentifierRules.IsValidTenantId(team.TeamId))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail =
                    "The team id must be 1 to 63 lowercase letters, digits or hyphens that starts and ends with a letter or digit.",
            };
        }

        if (string.IsNullOrWhiteSpace(team.DisplayName))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "The display name is required.",
            };
        }

        var store = storeManager.GetStore<ITeamStore>();

        var existing = await store.GetOrDefaultAsync(team.TeamId, cancellationToken);
        if (existing is not null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A team with the specified identifier already exists.",
            };
        }

        if (await store.IsTeamIdRetiredAsync(team.TeamId, cancellationToken))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The identifier belonged to a deleted team and cannot be reused.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        PersistedTeam team,
        UpdateTeamRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            ResourceNode.For(controlPlaneTenantId, ResourceNodeTypes.Team, team.TeamId),
            Operations.Update,
            "The caller is not authorized to update this team.",
            "Authentication is required to update this team."
        );
        if (error is not null)
        {
            return error;
        }

        if (model.IsDisabled != team.IsDisabled)
        {
            error = await AuthorizeAsync(
                user,
                ResourceNode.For(
                    controlPlaneTenantId,
                    ResourceNodeTypes.Platform,
                    OpenIdConstants.PlatformResourceId
                ),
                Operations.Update,
                "The caller is not authorized to disable or enable this team.",
                "Authentication is required to disable or enable this team."
            );
            if (error is not null)
            {
                return error;
            }
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, team.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status412PreconditionFailed,
                Detail = "The supplied If-Match token does not match the current team.",
            };
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "The display name is required.",
            };
        }

        if (
            model.IsDisabled
            && string.Equals(team.TeamId, Options.Root.TeamId, StringComparison.Ordinal)
        )
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The platform's own team cannot be disabled.",
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        PersistedTeam team,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var error = await AuthorizeAsync(
            user,
            ResourceNode.For(
                controlPlaneTenantId,
                ResourceNodeTypes.Platform,
                OpenIdConstants.PlatformResourceId
            ),
            Operations.Delete,
            "The caller is not authorized to delete this team.",
            "Authentication is required to delete this team."
        );
        if (error is not null)
        {
            return error;
        }

        if (string.Equals(team.TeamId, Options.Root.TeamId, StringComparison.Ordinal))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The platform's own team cannot be deleted.",
            };
        }

        var store = storeManager.GetStore<ITeamStore>();
        if (await store.HasDependentsAsync(team.TeamId, cancellationToken))
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The team cannot be deleted while it still has tenants.",
            };
        }

        return null;
    }
}
