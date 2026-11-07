// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/*

POST   api/teams
GET    api/teams
GET    api/teams/{teamId}
PATCH  api/teams/{teamId}
DELETE api/teams/{teamId}

POST api/teams/{teamId}/teams
GET  api/teams/{teamId}/teams

GET   api/teams/{teamId}/settings
GET   api/teams/{teamId}/settings/limits
PATCH api/teams/{teamId}/settings
GET   api/teams/{teamId}/effective-settings
GET   api/teams/{teamId}/usage

GET    api/teams/{teamId}/owners
POST   api/teams/{teamId}/owners
DELETE api/teams/{teamId}/owners/{principalId}

GET    api/teams/{teamId}/admins
POST   api/teams/{teamId}/admins
DELETE api/teams/{teamId}/admins/{principalId}

*/

/// <summary>
/// Provides the API endpoints for managing a team: a customer's grouping of related tenants. A team is an authority
/// node beneath the platform, and every team node carries the control-plane tenant as its tenant.
/// </summary>
// See ADR-1411
internal class TeamApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ITeamValidator teamValidator,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    ITeamClientCoherenceChecker teamClientCoherenceChecker,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<TeamApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <summary>
    /// Gets the most days one read of a team's usage spans: a year of the ledger's retention.
    /// </summary>
    internal const int MaxUsageDays = 366;

    /// <summary>
    /// Gets how many days before the last day a read of a team's usage starts when it names no first day.
    /// </summary>
    internal const int DefaultUsageDays = 30;

    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ITeamValidator TeamValidator { get; } = teamValidator;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private ITeamClientCoherenceChecker TeamClientCoherenceChecker { get; } =
        teamClientCoherenceChecker;

    /// <inheritdoc />
    protected override IManagementAuditRecorder ManagementAuditRecorder { get; } =
        managementAuditRecorder;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<TeamApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var teams = endpoints.MapGroup("/teams").WithTags(OpenIdConstants.EndpointTags.Teams);

        teams
            .MapPost("", CreateTeamAsync)
            .Produces<TeamResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        teams.MapGet("", ListTeamsAsync).Produces<CollectionResource<TeamResource>>();
        teams.MapGet("/{teamId}", GetTeamAsync).Produces<TeamResource>();
        teams
            .MapPatch("/{teamId}", UpdateTeamAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden);
        teams
            .MapDelete("/{teamId}", DeleteTeamAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        teams
            .MapPost("/{teamId}/teams", CreateChildTeamAsync)
            .Produces<TeamResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
        teams
            .MapGet("/{teamId}/teams", ListChildTeamsAsync)
            .Produces<CollectionResource<TeamResource>>();

        teams.MapGet("/{teamId}/settings", GetSettingsAsync).Produces<TeamSettingsResource>();
        teams
            .MapGet("/{teamId}/settings/limits", GetSettingLimitsAsync)
            .Produces<SettingLimitsResource>();
        teams
            .MapPatch("/{teamId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        teams
            .MapGet("/{teamId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<TeamEffectiveSettingsResource>();
        teams
            .MapGet("/{teamId}/usage", GetUsageAsync)
            .Produces<TeamUsageResource>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        teams
            .MapGet("/{teamId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        teams
            .MapPost("/{teamId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created)
            .Produces<OwnerResource>(StatusCodes.Status200OK);
        teams
            .MapDelete("/{teamId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict);

        teams
            .MapGet("/{teamId}/admins", ListAdminsAsync)
            .Produces<CollectionResource<OwnerResource>>();
        teams
            .MapPost("/{teamId}/admins", AddAdminAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created)
            .Produces<OwnerResource>(StatusCodes.Status200OK);
        teams
            .MapDelete("/{teamId}/admins/{principalId}", RemoveAdminAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    private static string GetControlPlaneTenantId(HttpContext httpContext) =>
        httpContext.GetOpenIdContext().Tenant.TenantId;

    /// <summary>
    /// Maps a <see cref="PersistedTeam"/> to its <see cref="TeamResource"/> representation.
    /// </summary>
    /// <param name="team">The <see cref="PersistedTeam"/> to map.</param>
    /// <returns>The mapped <see cref="TeamResource"/>.</returns>
    internal static TeamResource ToTeamResource(PersistedTeam team) =>
        new()
        {
            TeamId = team.TeamId,
            ConcurrencyToken = team.ConcurrencyToken,
            DisplayName = team.DisplayName,
            IsDisabled = team.IsDisabled,
            ParentTeamId = team.ParentTeamId,
        };

    /// <summary>
    /// Maps a <see cref="PersistedTeamSettings"/> to its <see cref="TeamSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedTeamSettings"/> to map.</param>
    /// <returns>The mapped <see cref="TeamSettingsResource"/>.</returns>
    internal virtual TeamSettingsResource ToTeamSettingsResource(PersistedTeamSettings settings) =>
        new()
        {
            TeamId = settings.TeamId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };

    /// <summary>
    /// Maps a team's effective (merged) <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="TeamEffectiveSettingsResource"/> representation: a flat JSON object of setting name to formatted
    /// value, ordered by name for a stable dump.
    /// </summary>
    /// <param name="teamId">The identifier of the team that owns the settings.</param>
    /// <param name="settings">The effective <see cref="IReadOnlySettingCollection"/> to map.</param>
    /// <returns>The mapped <see cref="TeamEffectiveSettingsResource"/>.</returns>
    internal virtual TeamEffectiveSettingsResource ToTeamEffectiveSettingsResource(
        string teamId,
        IReadOnlySettingCollection settings
    ) =>
        new()
        {
            TeamId = teamId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Team
            ),
        };

    /// <summary>
    /// Creates a team. The caller becomes its owner and administrator.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreateTeamRequest"/> describing the team to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created team.</response>
    [EndpointName("api/teams/create")]
    internal virtual async ValueTask<IResult> CreateTeamAsync(
        HttpContext httpContext,
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken
    ) => await CreateTeamCoreAsync(httpContext, parentTeamId: null, request, cancellationToken);

    /// <summary>
    /// Creates a team directly beneath a parent team. The caller becomes its owner and administrator.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the parent team.</param>
    /// <param name="request">The <see cref="CreateTeamRequest"/> describing the team to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created team.</response>
    /// <response code="403">The caller lacks <c>Create</c> at the parent team.</response>
    /// <response code="404">The parent team does not exist.</response>
    /// <response code="409">The parent is the platform's own team, the team would be nested too deep, or a team above it holds its maximum number of teams.</response>
    // See ADR-1424 and ADR-1426.
    [EndpointName("api/teams/teams/create")]
    internal virtual async ValueTask<IResult> CreateChildTeamAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken
    ) => await CreateTeamCoreAsync(httpContext, teamId, request, cancellationToken);

    private async ValueTask<IResult> CreateTeamCoreAsync(
        HttpContext httpContext,
        string? parentTeamId,
        CreateTeamRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var controlPlaneTenantId = openIdContext.Tenant.TenantId;

        var authorizationError = await TeamValidator.AuthorizeCreateAsync(
            httpContext.User,
            controlPlaneTenantId,
            parentTeamId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var teamId = request.TeamId;
        var team = new PersistedTeam
        {
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            IsDisabled = request.IsDisabled,
            ParentTeamId = parentTeamId,
            DisplayName = request.DisplayName,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
        };

        var error = await TeamValidator.ValidateCreateAsync(team, storeManager, cancellationToken);
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // A team carries only the settings that apply within it (ADR-1149): a settings document that cannot be honored
        // is rejected here rather than stored and failed on every request that resolves the team.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Team,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        if (parentTeamId is not null)
        {
            var limitError = await CheckTeamLimitsAsync(store, parentTeamId, cancellationToken);
            if (limitError is not null)
            {
                return ToErrorResult(limitError);
            }
        }

        await store.AddAsync(team, cancellationToken);

        // The team row commits first: the creator's assignments are rows the team owns, and the stores resolve an owning
        // resource from the database rather than the change tracker.
        await storeManager.SaveChangesAsync(cancellationToken);

        var creatorAssignment = await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            controlPlaneTenantId,
            ResourceNodeTypes.Team,
            team.TeamId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        // The creator administers the team and everything beneath it, so tenants are created under it.
        var owners = await ResourceOwnershipService.GetOwnersAsync(
            storeManager,
            ResourceNodeTypes.Team,
            team.TeamId,
            cancellationToken
        );
        var addedAdmins = new List<PersistedRoleAssignment>();
        foreach (var owner in owners)
        {
            var grant = await ResourceOwnershipService.AddAssignmentAsync(
                storeManager,
                controlPlaneTenantId,
                ResourceNodeTypes.Team,
                team.TeamId,
                owner.PrincipalId,
                BuiltInRoles.TeamAdmin,
                cancellationToken
            );
            if (grant.IsAdded)
            {
                addedAdmins.Add(grant.Assignment);
            }
        }
        await storeManager.SaveChangesAsync(cancellationToken);

        // The saves replace the team's concurrency token, so the response reflects the team as it now stands.
        team = await ReloadTeamAsync(team, cancellationToken);

        await ManagementAuditRecorder.RecordTeamChangedAsync(
            httpContext,
            team.TeamId,
            ResourceChangeTypes.Created,
            ToResourceValues(ToTeamResource(team))
        );

        if (creatorAssignment is not null)
        {
            await RecordRoleAssignmentChangedAsync(
                httpContext,
                creatorAssignment,
                ResourceChangeTypes.Created
            );
        }

        foreach (var admin in addedAdmins)
        {
            await RecordRoleAssignmentChangedAsync(httpContext, admin, ResourceChangeTypes.Created);
        }

        httpContext.Response.Headers.ETag = team.ConcurrencyToken;
        return TypedResults.Created($"/teams/{team.TeamId}", ToTeamResource(team));
    }

    /// <summary>
    /// Lists the teams.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of teams to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of teams.</response>
    [EndpointName("api/teams/list")]
    internal virtual async ValueTask<IResult> ListTeamsAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        // Listing teams is platform authority: a global admin or an assignment at the platform node lists them, and
        // nothing beneath the platform does.
        return await ProcessListAsync(
            httpContext,
            GetPlatformNode(httpContext),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITeamStore>();
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToTeamResource
        );
    }

    /// <summary>
    /// Lists the teams directly beneath a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the parent team.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of teams to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of the teams directly beneath the team; an empty page for a team that has none.</response>
    // See ADR-1424 and ADR-1426.
    [EndpointName("api/teams/teams/list")]
    internal virtual async ValueTask<IResult> ListChildTeamsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);
        var teamNode = GetTeamNode(httpContext, teamId);

        // Listing a team's child teams is authority at the team: the caller is authorized at the team node before the
        // store is read.
        return await ProcessListAsync(
            httpContext,
            teamNode,
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITeamStore>();
                return await store.GetChildPageAsync(
                    teamId,
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToTeamResource
        );
    }

    /// <summary>
    /// Gets a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested team.</response>
    [EndpointName("api/teams/get")]
    internal virtual async ValueTask<IResult> GetTeamAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessGetAsync(
            httpContext,
            GetTeamNode(httpContext, teamId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<ITeamStore>();
                return await store.GetOrDefaultAsync(teamId, token);
            },
            ToTeamResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="request">The JSON Patch document to apply to the team metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The team was updated.</response>
    /// <response code="403">The caller lacks authority at the team, or at the node above it.</response>
    [EndpointName("api/teams/update")]
    internal virtual async ValueTask<IResult> UpdateTeamAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] JsonPatchDocument<UpdateTeamRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await TeamValidator.AuthorizeUpdateAsync(
            httpContext.User,
            GetControlPlaneTenantId(httpContext),
            teamId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var team = await store.GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateTeamRequest
        {
            DisplayName = team.DisplayName,
            IsDisabled = team.IsDisabled,
        };

        try
        {
            request.ApplyTo(model);
        }
        catch (JsonPatchException exception)
        {
            Logger.JsonPatchFailed(exception);
            return TypedResults.Problem(
                detail: "The JSON Patch document could not be applied.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var error = await TeamValidator.ValidateUpdateAsync(
            httpContext.User,
            GetControlPlaneTenantId(httpContext),
            team,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        team.DisplayName = model.DisplayName ?? team.DisplayName;
        team.IsDisabled = model.IsDisabled;

        await store.UpdateAsync(team, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTeamChangedAsync(
            httpContext,
            team.TeamId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToTeamResource(team))
        );

        await NotifyTeamChangedAsync(
            httpContext,
            team.TeamId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The team was deleted.</response>
    /// <response code="403">The caller lacks authority at the team, or at the node above it.</response>
    /// <response code="409">The team is the platform's own team or still has tenants or teams, and cannot be deleted.</response>
    [EndpointName("api/teams/delete")]
    internal virtual async ValueTask<IResult> DeleteTeamAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await TeamValidator.AuthorizeDeleteAsync(
            httpContext.User,
            GetControlPlaneTenantId(httpContext),
            teamId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var team = await store.GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var error = await TeamValidator.ValidateDeleteAsync(
            httpContext.User,
            GetControlPlaneTenantId(httpContext),
            team,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var resourceValues = ToResourceValues(ToTeamResource(team));

        await store.RemoveAsync(teamId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTeamChangedAsync(
            httpContext,
            team.TeamId,
            ResourceChangeTypes.Deleted,
            resourceValues
        );

        await NotifyTeamChangedAsync(
            httpContext,
            team.TeamId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a team's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The team's settings.</response>
    [EndpointName("api/teams/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessGetAsync(
            httpContext,
            GetTeamNode(httpContext, teamId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<ITeamStore>();
                return await store.GetSettingsOrDefaultAsync(teamId, token);
            },
            ToTeamSettingsResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the limits a team's settings carry and whether the caller may set them: whether it holds update authority
    /// over the team's parent team, or over the platform for a top-level team.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The team's setting limits.</response>
    // See ADR-1448.
    [EndpointName("api/teams/settings/limits/get")]
    internal virtual async ValueTask<IResult> GetSettingLimitsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        async ValueTask<ResourceNode?> ReadNodeAboveAsync(CancellationToken token)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(token);
            var store = storeManager.GetStore<ITeamStore>();
            var team = await store.GetOrDefaultAsync(teamId, token);
            if (team is null)
            {
                return null;
            }

            var controlPlaneTenantId = GetControlPlaneTenantId(httpContext);
            return TeamNodes.GetNodeAbove(controlPlaneTenantId, team.ParentTeamId);
        }

        var limitNames = SettingSerializer.GetLimits(OpenIdSettingScopes.Team);
        var teamNode = GetTeamNode(httpContext, teamId);

        return await ProcessGetSettingLimitsAsync(
            httpContext,
            teamNode,
            limitNames,
            ReadNodeAboveAsync,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets a team's effective (resolved) settings: the platform's settings, then each team's above it, with the team's
    /// document merged onto them.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The team's effective settings.</response>
    [EndpointName("api/teams/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            GetTeamNode(httpContext, teamId),
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<ITeamStore>();
                var team = await store.GetOrDefaultAsync(teamId, token);
                return team?.TeamId;
            },
            async (_, token) =>
            {
                var effectiveSettings = await EffectiveSettingsResolver.ResolveTeamAsync(
                    teamId,
                    token
                );
                return ToTeamEffectiveSettingsResource(teamId, effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a team's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The team's settings were updated.</response>
    [EndpointName("api/teams/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            GetTeamNode(httpContext, teamId),
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();

        var team = await store.GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var teamSettings = team.Settings;

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, teamSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(teamSettings.Value);

        try
        {
            request.ApplyTo(jsonObject);
        }
        catch (JsonPatchException exception)
        {
            Logger.JsonPatchFailed(exception);
            return TypedResults.Problem(
                detail: "The JSON Patch document could not be applied.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var settingsValue = SerializeToElement(jsonObject);
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            settingsValue,
            OpenIdSettingScopes.Team,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var changedLimits = SettingSerializer.GetChangedLimits(teamSettings.Value, settingsValue);
        // A limit in a team's document is set by the authority over its parent team, or over the platform for a
        // top-level team (ADR-1418, ADR-1426).
        var authorityAbove = TeamNodes.GetNodeAbove(
            GetControlPlaneTenantId(httpContext),
            team.ParentTeamId
        );
        var limitRefusal = await GetLimitChangeRefusalOrDefaultAsync(
            httpContext,
            changedLimits,
            authorityAbove,
            cancellationToken
        );
        if (limitRefusal is not null)
        {
            return limitRefusal;
        }

        // The parent's settings are resolved outside the replaced document's catch, so a document above the team that
        // cannot be honored fails the write naming its owner (ADR-1245).
        var parentSettings = await EffectiveSettingsResolver.ResolveTeamParentAsync(
            teamId,
            cancellationToken
        );
        var newTeamSettings = EffectiveSettingsResolver.Resolve(
            parentSettings,
            OpenIdSettingScopes.Team,
            settingsValue
        );
        var currentTeamSettings = ResolveReplacedTeamSettingsOrDefault(
            teamId,
            parentSettings,
            teamSettings.Value
        );

        if (
            currentTeamSettings is not null
            && ClientCoherenceValidator.ReadsChangedSetting(currentTeamSettings, newTeamSettings)
        )
        {
            var coherenceErrors = await ValidateClientCoherenceAsync(
                teamId,
                currentTeamSettings,
                newTeamSettings,
                storeManager,
                cancellationToken
            );
            if (coherenceErrors.Count > 0)
            {
                return SettingsValidationProblem(coherenceErrors);
            }
        }

        teamSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(teamSettings, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTeamChangedAsync(
            httpContext,
            teamSettings.TeamId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToTeamSettingsResource(teamSettings))
        );

        await NotifyTeamChangedAsync(
            httpContext,
            teamSettings.TeamId,
            settingsChanged: true,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = teamSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves the effective settings of the team's committed settings document, which a settings write replaces, onto
    /// the effective settings of the team's parent that the caller resolved. When the document cannot be honored by the
    /// settings vocabulary, nothing was coherent under it, so no client is checked and the write may replace it with a
    /// document that can be honored.
    /// </summary>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="parentSettings">The effective settings of the team's parent team, or of the platform for a top-level team.</param>
    /// <param name="replacedSettings">The team's committed settings document.</param>
    /// <returns>The team's effective settings, or <see langword="null"/> when the document cannot be honored.</returns>
    // See ADR-1241.
    private IReadOnlySettingCollection? ResolveReplacedTeamSettingsOrDefault(
        string teamId,
        IReadOnlySettingCollection parentSettings,
        JsonElement replacedSettings
    )
    {
        try
        {
            return EffectiveSettingsResolver.Resolve(
                parentSettings,
                OpenIdSettingScopes.Team,
                replacedSettings
            );
        }
        catch (JsonException exception)
        {
            Logger.TeamSettingsUnreadable(teamId, exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Verifies that replacing the team's settings document leaves every value each client registers permitted by every
    /// client coherence rule, in every tenant of the team and of every team beneath it, through the
    /// <see cref="ITeamClientCoherenceChecker"/>. The current and new team settings are each resolved through the
    /// <see cref="IEffectiveSettingsResolver"/>.
    /// </summary>
    /// <param name="teamId">The identifier of the team whose settings change.</param>
    /// <param name="currentTeamSettings">The team's effective settings with its current settings document.</param>
    /// <param name="newTeamSettings">The team's effective settings with its new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenants and their clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the team setting that does not permit each value, or
    /// an empty list.</returns>
    // See ADR-1206 and ADR-1241.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        string teamId,
        IReadOnlySettingCollection currentTeamSettings,
        IReadOnlySettingCollection newTeamSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        return await TeamClientCoherenceChecker.FindIntroducedViolationsAsync(
            storeManager,
            teamId,
            currentTeamSettings,
            newTeamSettings,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets a team's usage from the durable daily ledger: the count of each meter of each tenant of the team and of every
    /// team beneath it on each UTC calendar day of a range, which the platform bills a plan by and reports to the
    /// customer. The ledger keeps the usage of deleted tenants and teams, so it can still be read.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="from">The first UTC calendar day, inclusive; when omitted, 30 days before <paramref name="to"/>.</param>
    /// <param name="to">The last UTC calendar day, inclusive; when omitted, the current UTC day.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The team's usage on the range.</response>
    /// <response code="400">The range is reversed, longer than <see cref="MaxUsageDays"/> days, or would start before the
    /// first representable day.</response>
    /// <response code="403">The caller lacks <c>Read</c> at the team, for any team identifier.</response>
    /// <response code="404">The team does not exist.</response>
    // See ADR-1199, ADR-1424 and ADR-1426.
    [EndpointName("api/teams/usage/get")]
    internal virtual async ValueTask<IResult> GetUsageAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken
    )
    {
        var teamNode = GetTeamNode(httpContext, teamId);
        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            teamNode,
            Operations.Read,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        var lastDay = to ?? DateOnly.FromDateTime(TimeProvider.GetUtcNow().UtcDateTime);
        if (from is null && lastDay.DayNumber < DefaultUsageDays)
        {
            // The default range would start before DateOnly.MinValue, which AddDays refuses by throwing.
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["to"] =
                    [
                        $"Without 'from', 'to' must be at least {DefaultUsageDays} days after the first representable day.",
                    ],
                }
            );
        }

        var firstDay = from ?? lastDay.AddDays(-DefaultUsageDays);
        if (firstDay > lastDay || lastDay.DayNumber - firstDay.DayNumber >= MaxUsageDays)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["from"] =
                    [
                        $"The range must run forward from 'from' to 'to' and span at most {MaxUsageDays} days.",
                    ],
                }
            );
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var teamStore = storeManager.GetStore<ITeamStore>();
        var team = await teamStore.GetOrDefaultAsync(teamId, cancellationToken);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        var teamPath = TeamPaths.Format([.. team.AncestorTeamIds, team.TeamId]);
        var store = storeManager.GetStore<ITenantUsageStore>();
        var usage = await store.ListByTeamAsync(teamPath, firstDay, lastDay, cancellationToken);

        return TypedResults.Ok(
            new TeamUsageResource
            {
                TeamId = teamId,
                From = firstDay,
                To = lastDay,
                Entries = usage
                    .Select(entry => new TeamUsageEntry
                    {
                        TeamId = TeamPaths.Parse(entry.TeamPath)[^1],
                        TenantId = entry.TenantId,
                        Day = entry.Day,
                        Meter = entry.Meter,
                        Count = entry.Count,
                    })
                    .ToList(),
            }
        );
    }

    /// <summary>
    /// Lists a team's owners.
    /// </summary>
    /// <response code="200">The team's owners.</response>
    [EndpointName("api/teams/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    ) =>
        await ProcessListOwnersAsync(
            httpContext,
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Team,
            teamId,
            (storeManager, token) => TeamExistsAsync(storeManager, teamId, token),
            cancellationToken
        );

    /// <summary>
    /// Adds an owner to a team.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    /// <response code="200">The principal already owns the team.</response>
    [EndpointName("api/teams/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    ) =>
        await ProcessAddOwnerAsync(
            httpContext,
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Team,
            teamId,
            request,
            $"/teams/{teamId}/owners",
            (storeManager, token) => TeamExistsAsync(storeManager, teamId, token),
            cancellationToken
        );

    /// <summary>
    /// Removes an owner from a team.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    /// <response code="409">The owner is the team's last owner.</response>
    [EndpointName("api/teams/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    ) =>
        await ProcessRemoveOwnerAsync(
            httpContext,
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Team,
            teamId,
            principalId,
            cancellationToken
        );

    /// <summary>
    /// Lists a team's administrators: the principals that manage the team and everything beneath it.
    /// </summary>
    /// <response code="200">The team's administrators.</response>
    [EndpointName("api/teams/admins/list")]
    internal virtual async ValueTask<IResult> ListAdminsAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetTeamNode(httpContext, teamId),
            Operations.Read
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        if (!await TeamExistsAsync(storeManager, teamId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var admins = await ResourceOwnershipService.GetAssignmentsAsync(
            storeManager,
            ResourceNodeTypes.Team,
            teamId,
            BuiltInRoles.TeamAdmin,
            cancellationToken
        );
        return TypedResults.Json(
            new CollectionResource<OwnerResource>
            {
                Items = admins.Select(ToOwnerResource).ToList(),
                ContinuationToken = null,
            }
        );
    }

    /// <summary>
    /// Adds an administrator to a team. Granting the role that creates tenants is creation authority, so only a
    /// team administrator or a global administrator at the team or above can grant it.
    /// </summary>
    /// <response code="201">The administrator was added.</response>
    /// <response code="200">The principal already administers the team.</response>
    [EndpointName("api/teams/admins/add")]
    internal virtual async ValueTask<IResult> AddAdminAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetTeamNode(httpContext, teamId),
            Operations.Create
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (string.IsNullOrEmpty(request.PrincipalId))
        {
            return TypedResults.Problem(
                detail: "The 'principalId' is required.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        if (!await TeamExistsAsync(storeManager, teamId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var grant = await ResourceOwnershipService.AddAssignmentAsync(
            storeManager,
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Team,
            teamId,
            request.PrincipalId,
            BuiltInRoles.TeamAdmin,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        var ownerResource = ToOwnerResource(grant.Assignment);
        if (!grant.IsAdded)
        {
            return TypedResults.Ok(ownerResource);
        }

        await RecordRoleAssignmentChangedAsync(
            httpContext,
            grant.Assignment,
            ResourceChangeTypes.Created
        );

        return TypedResults.Created(
            $"/teams/{teamId}/admins/{grant.Assignment.PrincipalId}",
            ownerResource
        );
    }

    /// <summary>
    /// Removes an administrator from a team. Revoking the role that creates tenants is creation authority, so only a
    /// team administrator or a global administrator at the team or above can revoke it.
    /// </summary>
    /// <response code="204">The administrator was removed.</response>
    [EndpointName("api/teams/admins/remove")]
    internal virtual async ValueTask<IResult> RemoveAdminAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetTeamNode(httpContext, teamId),
            Operations.Create
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var removed = await ResourceOwnershipService.RemoveAssignmentAsync(
            storeManager,
            ResourceNodeTypes.Team,
            teamId,
            principalId,
            BuiltInRoles.TeamAdmin,
            cancellationToken
        );
        if (removed.Count == 0)
        {
            return TypedResults.NotFound();
        }

        await storeManager.SaveChangesAsync(cancellationToken);
        foreach (var assignment in removed)
        {
            await RecordRoleAssignmentChangedAsync(
                httpContext,
                assignment,
                ResourceChangeTypes.Deleted
            );
        }

        return TypedResults.NoContent();
    }

    // Announces a committed change to the team: this replica evicts it and its tenants from the caches, so the change
    // takes effect at once, and after a settings change reconciles the team. The mediator is request-scoped, so it is
    // resolved from the request's services.
    private static ValueTask NotifyTeamChangedAsync(
        HttpContext httpContext,
        string teamId,
        bool settingsChanged,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new TeamChangedCommand(teamId, settingsChanged),
            cancellationToken
        );
    }

    // Checks the limits a child team's creation is held to: the platform's 'max_team_depth' bounds how deep the child
    // sits, and every team above it holds at most its 'max_teams' teams beneath it (ADR-1426). Unset 'max_teams'
    // means unlimited, and an unset depth falls back to the hard bound.
    private async ValueTask<ManagementError?> CheckTeamLimitsAsync(
        ITeamStore store,
        string parentTeamId,
        CancellationToken cancellationToken
    )
    {
        var lineage = await store.GetLineageAsync(parentTeamId, cancellationToken);

        var platformSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
            cancellationToken
        );
        var maxDepth = platformSettings.TryGetValue(
            OpenIdSettingKeys.MaxTeamDepth,
            out var configuredDepth
        )
            ? configuredDepth
            : TeamPaths.MaxDepth;
        if (lineage.Count + 1 > maxDepth)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = $"A team can be nested at most {maxDepth} levels deep.",
            };
        }

        var settings = platformSettings;
        foreach (var ancestor in lineage)
        {
            settings = EffectiveSettingsResolver.ResolveStored(
                settings,
                OpenIdSettingScopes.Team,
                ancestor.TeamId,
                ancestor.Settings.Value
            );
            if (
                settings.TryGetValue(OpenIdSettingKeys.MaxTeams, out var maxTeams)
                && await store.CountDescendantsAsync(ancestor.TeamId, cancellationToken) >= maxTeams
            )
            {
                return new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = $"The team '{ancestor.TeamId}' holds its maximum of {maxTeams} teams.",
                };
            }
        }

        return null;
    }

    private async ValueTask<PersistedTeam> ReloadTeamAsync(
        PersistedTeam team,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITeamStore>();
        return await store.GetOrDefaultAsync(team.TeamId, cancellationToken) ?? team;
    }

    private static async ValueTask<bool> TeamExistsAsync(
        IStoreManager storeManager,
        string teamId,
        CancellationToken cancellationToken
    ) =>
        await storeManager.GetStore<ITeamStore>().GetOrDefaultAsync(teamId, cancellationToken)
            is not null;
}
