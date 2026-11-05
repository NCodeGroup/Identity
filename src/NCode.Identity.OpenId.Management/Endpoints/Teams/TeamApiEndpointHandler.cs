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
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Logging;
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

GET   api/teams/{teamId}/settings
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
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<TeamApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    // The number of tenants read per page when a settings write is checked against the team's tenants' clients.
    private const int TenantPageSize = 100;

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
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

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
        teams.MapPatch("/{teamId}", UpdateTeamAsync).Produces(StatusCodes.Status204NoContent);
        teams.MapDelete("/{teamId}", DeleteTeamAsync).Produces(StatusCodes.Status204NoContent);

        teams.MapGet("/{teamId}/settings", GetSettingsAsync).Produces<TeamSettingsResource>();
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
            .ProducesValidationProblem();

        teams
            .MapGet("/{teamId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        teams
            .MapPost("/{teamId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        teams
            .MapDelete("/{teamId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);

        teams
            .MapGet("/{teamId}/admins", ListAdminsAsync)
            .Produces<CollectionResource<OwnerResource>>();
        teams
            .MapPost("/{teamId}/admins", AddAdminAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        teams
            .MapDelete("/{teamId}/admins/{principalId}", RemoveAdminAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    // The platform's authority node, addressed from the control-plane tenant the request runs under.
    private static ResourceNode GetPlatformNode(HttpContext httpContext) =>
        ResourceNode.For(
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId
        );

    // The team's authority node, addressed from the control-plane tenant the request runs under.
    private static ResourceNode GetTeamNode(HttpContext httpContext, string teamId) =>
        ResourceNode.For(GetControlPlaneTenantId(httpContext), ResourceNodeTypes.Team, teamId);

    private static string GetControlPlaneTenantId(HttpContext httpContext) =>
        httpContext.GetOpenIdContext().Tenant.TenantId;

    /// <summary>
    /// Maps a <see cref="PersistedTeam"/> to its <see cref="TeamResource"/> representation.
    /// </summary>
    /// <param name="team">The <see cref="PersistedTeam"/> to map.</param>
    /// <returns>The mapped <see cref="TeamResource"/>.</returns>
    internal virtual TeamResource ToTeamResource(PersistedTeam team) =>
        new()
        {
            TeamId = team.TeamId,
            ConcurrencyToken = team.ConcurrencyToken,
            DisplayName = team.DisplayName,
            IsDisabled = team.IsDisabled,
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
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var controlPlaneTenantId = openIdContext.Tenant.TenantId;

        var authorizationError = await TeamValidator.AuthorizeCreateAsync(
            httpContext.User,
            controlPlaneTenantId,
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

        await store.AddAsync(team, cancellationToken);

        // The team row commits first: the creator's assignments are rows the team owns, and the stores resolve an owning
        // resource from the database rather than the change tracker.
        await storeManager.SaveChangesAsync(cancellationToken);

        await ResourceOwnershipService.AssignCreatorAsync(
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
        foreach (var owner in owners)
        {
            await ResourceOwnershipService.AddAssignmentAsync(
                storeManager,
                controlPlaneTenantId,
                ResourceNodeTypes.Team,
                team.TeamId,
                owner.PrincipalId,
                BuiltInRoles.TeamAdmin,
                cancellationToken
            );
        }
        await storeManager.SaveChangesAsync(cancellationToken);

        // The saves replace the team's concurrency token, so the response reflects the team as it now stands.
        team = await ReloadTeamAsync(team, cancellationToken);

        await ManagementAuditRecorder.RecordTeamChangedAsync(
            httpContext,
            team.TeamId,
            ResourceChangeTypes.Created,
            ToResourceValues(ToTeamResource(team)),
            cancellationToken
        );

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
            ToResourceValues(ToTeamResource(team)),
            cancellationToken
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
    /// <response code="409">The team is the platform's own team or still has tenants, and cannot be deleted.</response>
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

        var error = await TeamValidator.ValidateDeleteAsync(team, storeManager, cancellationToken);
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
            resourceValues,
            cancellationToken
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
    /// Gets a team's effective (resolved) settings: the platform's settings with the team's document merged onto them.
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

        var teamSettings = await store.GetSettingsOrDefaultAsync(teamId, cancellationToken);
        if (teamSettings is null)
        {
            return TypedResults.NotFound();
        }

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
        var authorityAbove = GetPlatformNode(httpContext);
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

        // The platform is resolved outside the replaced document's catch, so a platform document that cannot be honored
        // fails the write naming the platform (ADR-1245).
        var platformSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
            cancellationToken
        );
        var newTeamSettings = EffectiveSettingsResolver.Resolve(
            platformSettings,
            OpenIdSettingScopes.Team,
            settingsValue
        );
        var currentTeamSettings = ResolveReplacedTeamSettingsOrDefault(
            teamId,
            platformSettings,
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
            ToResourceValues(ToTeamSettingsResource(teamSettings)),
            cancellationToken
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
    /// the platform's effective settings that the caller resolved. When the document cannot be honored by the settings
    /// vocabulary, nothing was coherent under it, so no client is checked and the write may replace it with a document
    /// that can be honored.
    /// </summary>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="platformSettings">The platform's effective settings, which are the team's parent settings.</param>
    /// <param name="replacedSettings">The team's committed settings document.</param>
    /// <returns>The team's effective settings, or <see langword="null"/> when the document cannot be honored.</returns>
    // See ADR-1241.
    private IReadOnlySettingCollection? ResolveReplacedTeamSettingsOrDefault(
        string teamId,
        IReadOnlySettingCollection platformSettings,
        JsonElement replacedSettings
    )
    {
        try
        {
            return EffectiveSettingsResolver.Resolve(
                platformSettings,
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
    /// Verifies that replacing the team's settings document leaves every value each client of each of the team's tenants
    /// registers permitted by every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The current and
    /// new team settings are each resolved through the <see cref="IEffectiveSettingsResolver"/>, and each tenant's
    /// document is resolved onto both. Only the team's tenants are read, as an untracked, secret-free projection
    /// (<see cref="ITenantStore.GetSettingsPageAsync(string, PageRequest, CancellationToken)"/>). A tenant whose
    /// settings document cannot be honored is logged and skipped, because none of its clients is coherent under either
    /// settings.
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
        List<SettingError> errors = [];
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(teamId, page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                IReadOnlySettingCollection currentTenantSettings;
                IReadOnlySettingCollection newTenantSettings;
                try
                {
                    currentTenantSettings = EffectiveSettingsResolver.Resolve(
                        currentTeamSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    newTenantSettings = EffectiveSettingsResolver.Resolve(
                        newTeamSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                }
                catch (JsonException exception)
                {
                    Logger.TenantSettingsUnreadable(tenantSettings.TenantId, exception.Message);
                    continue;
                }

                var findings = await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
                    storeManager,
                    tenantSettings.TenantId,
                    currentTenantSettings,
                    newTenantSettings,
                    cancellationToken
                );

                errors.AddRange(
                    findings.Select(finding => new SettingError(
                        finding.Violation.GoverningSettingName,
                        $"The change would make the client '{finding.ClientId}' of tenant '{tenantSettings.TenantId}' incoherent: {finding.Violation.Description}"
                    ))
                );
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);

        return errors;
    }

    /// <summary>
    /// Gets a team's usage from the durable daily ledger: the count of each meter of each of the team's tenants on each UTC
    /// calendar day of a range, which the platform bills a plan by and reports to the customer. The ledger keeps a
    /// deleted tenant's usage, so it can still be read.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <param name="from">The first UTC calendar day, inclusive; when omitted, 30 days before <paramref name="to"/>.</param>
    /// <param name="to">The last UTC calendar day, inclusive; when omitted, the current UTC day.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The team's usage on the range.</response>
    /// <response code="400">The range is reversed, longer than <see cref="MaxUsageDays"/> days, or would start before the
    /// first representable day.</response>
    // See ADR-1199.
    [EndpointName("api/teams/usage/get")]
    internal virtual async ValueTask<IResult> GetUsageAsync(
        HttpContext httpContext,
        [FromRoute] string teamId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
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
        var store = storeManager.GetStore<ITenantUsageStore>();
        var usage = await store.ListByTeamAsync(teamId, firstDay, lastDay, cancellationToken);

        return TypedResults.Ok(
            new TeamUsageResource
            {
                TeamId = teamId,
                From = firstDay,
                To = lastDay,
                Entries = usage
                    .Select(entry => new TeamUsageEntry
                    {
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

        var admin = await ResourceOwnershipService.AddAssignmentAsync(
            storeManager,
            GetControlPlaneTenantId(httpContext),
            ResourceNodeTypes.Team,
            teamId,
            request.PrincipalId,
            BuiltInRoles.TeamAdmin,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/teams/{teamId}/admins/{admin.PrincipalId}",
            ToOwnerResource(admin)
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
        if (!removed)
        {
            return TypedResults.NotFound();
        }

        await storeManager.SaveChangesAsync(cancellationToken);
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
