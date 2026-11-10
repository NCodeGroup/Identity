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
/// Provides the default implementation of <see cref="ITeamValidator"/>. Creating a team is authorized at the platform,
/// or at the parent team the child-team route names, before any read; updating and deleting are authorized at the
/// team's own node before the team is read, and disabling, restoring or deleting it is then judged against the stored
/// row at the node above it, the parent team for a child team and the platform for a top-level team. A team's
/// identifier follows the form of a tenant identifier, is never reused after the team is deleted, and the platform's
/// own team can be neither disabled, deleted nor hold teams.
/// </summary>
// See ADR-1411, ADR-1424 and ADR-1426.
internal class DefaultTeamValidator(
    IAuthorizationService authorizationService,
    IOptions<TenantResolutionOptions> tenantResolutionOptions
) : DefaultResourceValidator(authorizationService), ITeamValidator
{
    private IOptions<TenantResolutionOptions> TenantResolutionOptions { get; } =
        tenantResolutionOptions;

    private TenantResolutionOptions Options => TenantResolutionOptions.Value;

    /// <summary>
    /// Determines whether the specified team identifier names the platform's own team.
    /// </summary>
    private bool IsPlatformTeam(string teamId) =>
        // Team identifiers compare without regard to case, as the store looks them up.
        string.Equals(teamId, Options.Root.TeamId, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string? parentTeamId,
        CancellationToken cancellationToken
    )
    {
        var node = TeamNodes.GetNodeAbove(controlPlaneTenantId, parentTeamId);
        return AuthorizeAsync(
            user,
            node,
            Operations.Create,
            "The caller is not authorized to create this team.",
            "Authentication is required to create this team.",
            cancellationToken
        );
    }

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string teamId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(controlPlaneTenantId, ResourceNodeTypes.Team, teamId),
            Operations.Update,
            "The caller is not authorized to update this team.",
            "Authentication is required to update this team.",
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string teamId,
        CancellationToken cancellationToken
    ) =>
        AuthorizeAsync(
            user,
            ResourceNode.For(controlPlaneTenantId, ResourceNodeTypes.Team, teamId),
            Operations.Delete,
            "The caller is not authorized to delete this team.",
            "Authentication is required to delete this team.",
            cancellationToken
        );

    /// <inheritdoc />
    public async ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedTeam team,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
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

        if (team.ParentTeamId is { } parentTeamId)
        {
            if (IsPlatformTeam(parentTeamId))
            {
                return new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "The platform's own team cannot hold teams.",
                };
            }

            var parent = await store.GetOrDefaultAsync(parentTeamId, cancellationToken);
            if (parent is null)
            {
                return new ManagementError
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    Detail = "The parent team does not exist.",
                };
            }
        }

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
        if (model.IsDisabled != team.IsDisabled)
        {
            var lifecycleNode = TeamNodes.GetNodeAbove(controlPlaneTenantId, team.ParentTeamId);
            var error = await AuthorizeAsync(
                user,
                lifecycleNode,
                Operations.Update,
                "The caller is not authorized to disable or enable this team.",
                "Authentication is required to disable or enable this team.",
                cancellationToken
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

        if (model.IsDisabled && IsPlatformTeam(team.TeamId))
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
        var lifecycleNode = TeamNodes.GetNodeAbove(controlPlaneTenantId, team.ParentTeamId);
        var authorizationError = await AuthorizeAsync(
            user,
            lifecycleNode,
            Operations.Delete,
            "The caller is not authorized to delete this team.",
            "Authentication is required to delete this team.",
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        if (IsPlatformTeam(team.TeamId))
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
                Detail =
                    "The team cannot be deleted while it still has tenants, teams or machine clients.",
            };
        }

        return null;
    }
}
