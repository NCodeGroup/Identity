// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.AspNetCore;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/*

GET    api/tenants/{tenantId}/resource-servers
POST   api/tenants/{tenantId}/resource-servers
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}
PATCH  api/tenants/{tenantId}/resource-servers/{resourceServerId}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}

GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes
POST   api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}
PUT    api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{scopeValue}

GET   api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings
GET   api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings/limits
PATCH api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings
GET   api/tenants/{tenantId}/resource-servers/{resourceServerId}/effective-settings

GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets
POST   api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}
GET    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}/public-key
PUT    api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}

*/

/// <summary>
/// Provides the API endpoints for managing resource servers (APIs, in Auth0 terms), including their scopes, settings,
/// and secrets.
/// </summary>
internal class ResourceServerApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IResourceServerValidator resourceServerValidator,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IClientKeyImporter keyImporter,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    ISecretProtectorProvider secretProtectorProvider,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<ResourceServerApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IResourceServerValidator ResourceServerValidator { get; } = resourceServerValidator;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter KeyImporter { get; } = keyImporter;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    /// <inheritdoc />
    protected override IManagementAuditRecorder ManagementAuditRecorder { get; } =
        managementAuditRecorder;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<ResourceServerApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var resourceServers = endpoints
            .MapGroup("/resource-servers")
            .WithTags(OpenIdConstants.EndpointTags.ResourceServers);

        resourceServers
            .MapGet("", ListAsync)
            .Produces<CollectionResource<ResourceServerResource>>();
        resourceServers
            .MapPost("", CreateAsync)
            .Produces<ResourceServerResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        resourceServers.MapGet("/{resourceServerId}", GetAsync).Produces<ResourceServerResource>();
        resourceServers
            .MapPatch("/{resourceServerId}", UpdateAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/scopes", ListScopesAsync)
            .Produces<IReadOnlyList<ScopeResource>>();
        resourceServers
            .MapPost("/{resourceServerId}/scopes", CreateScopeAsync)
            .Produces<ScopeResource>(StatusCodes.Status201Created);
        resourceServers
            .MapGet("/{resourceServerId}/scopes/{scopeValue}", GetScopeAsync)
            .Produces<ScopeResource>();
        resourceServers
            .MapPut("/{resourceServerId}/scopes/{scopeValue}", UpdateScopeAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}/scopes/{scopeValue}", DeleteScopeAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/settings", GetSettingsAsync)
            .Produces<ResourceServerSettingsResource>();
        resourceServers
            .MapGet("/{resourceServerId}/settings/limits", GetSettingLimitsAsync)
            .Produces<SettingLimitsResource>();
        resourceServers
            .MapPatch("/{resourceServerId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        resourceServers
            .MapGet("/{resourceServerId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<ResourceServerEffectiveSettingsResource>();

        resourceServers
            .MapGet("/{resourceServerId}/secrets", GetSecretsAsync)
            .Produces<ResourceServerSecretsResource>();
        // The response reveals a secret once, so no cache may keep it (ADR-1345).
        resourceServers
            .MapPost("/{resourceServerId}/secrets", CreateSecretAsync)
            .Produces<CreatedSecretResource>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .WithNoStore();
        resourceServers
            .MapPost("/{resourceServerId}/secrets/import", ImportSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);
        resourceServers
            .MapGet("/{resourceServerId}/secrets/{secretId}", GetSecretAsync)
            .Produces<SecretResource>();
        resourceServers
            .MapGet("/{resourceServerId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        resourceServers
            .MapPut("/{resourceServerId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        resourceServers
            .MapDelete("/{resourceServerId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);

        resourceServers
            .MapGet("/{resourceServerId}/owners", ListOwnersAsync)
            .Produces<RoleListResource>();
        resourceServers
            .MapPost("/{resourceServerId}/owners", AddOwnerAsync)
            .Produces<RoleHolderResource>(StatusCodes.Status201Created)
            .Produces<RoleHolderResource>(StatusCodes.Status200OK);
        resourceServers
            .MapDelete("/{resourceServerId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);
    }

    // A system resource server's tokens are read by the server itself, which decrypts them with the tenant's keys; a
    // key of its own would make issuance encrypt its tokens to a key the OpenID Provider's own endpoints do not read
    // (ADR-1167).
    private static ProblemHttpResult SystemResourceServerHoldsNoKeys() =>
        TypedResults.Problem(
            detail: "A system resource server is read by the server itself and holds no keys.",
            statusCode: StatusCodes.Status409Conflict
        );

    internal virtual ScopeResource ToScopeResource(PersistedScope scope) =>
        new()
        {
            Value = scope.Value,
            Description = scope.Description,
            IsSystem = scope.IsSystem,
        };

    internal virtual ResourceServerResource ToResourceServerResource(
        PersistedResourceServer resourceServer
    ) =>
        new()
        {
            TenantId = resourceServer.TenantId,
            ResourceServerId = resourceServer.ResourceServerId,
            Identifier = resourceServer.Identifier,
            ConcurrencyToken = resourceServer.ConcurrencyToken,
            Name = resourceServer.Name,
            IsSystem = resourceServer.IsSystem,
            IsDisabled = resourceServer.IsDisabled,
            Scopes = resourceServer.Scopes.Select(ToScopeResource).ToList(),
        };

    /// <summary>
    /// Lists the resource servers (APIs) in a tenant.
    /// </summary>
    /// <response code="200">A page of resource servers.</response>
    [EndpointName("api/resource-servers/list")]
    internal virtual async ValueTask<IResult> ListAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        return await ProcessListAsync(
            httpContext,
            TenantScopeResource.For(tenantId),
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IResourceServerStore>();
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToResourceServerResource
        );
    }

    /// <summary>
    /// Gets a resource server (API).
    /// </summary>
    /// <response code="200">The requested resource server.</response>
    [EndpointName("api/resource-servers/get")]
    internal virtual async ValueTask<IResult> GetAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();
                return await store.GetOrDefaultAsync(resourceServerId, token);
            },
            ToResourceServerResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a resource server (API).
    /// </summary>
    /// <response code="201">The created resource server.</response>
    [EndpointName("api/resource-servers/create")]
    internal virtual async ValueTask<IResult> CreateAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateResourceServerRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        // Authorization is asserted from the route, before any row is read (ADR-1424, ADR-1451).
        var authorizationError = await ResourceServerValidator.AuthorizeCreateAsync(
            httpContext.User,
            tenantId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerId = CryptoService.GenerateResourceId();

        var resourceServer = new PersistedResourceServer
        {
            TenantId = tenantId,
            ResourceServerId = resourceServerId,
            Identifier = request.Identifier,
            ConcurrencyToken = string.Empty,
            Name = request.Name,
            IsSystem = false,
            IsDisabled = request.IsDisabled,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = request.Settings,
            },
            Scopes = [],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = tenantId,
                ResourceServerId = resourceServerId,
                Value = [],
            },
        };

        // Core preconditions (identifier uniqueness) are asserted by the validator (ADR-1014).
        var error = await ResourceServerValidator.ValidateCreateAsync(
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // Setting names are a closed vocabulary (ADR-1139), and a resource server carries only the settings that apply
        // within it (ADR-1149): a settings document that cannot be honored is rejected here rather than stored and
        // failed when the resource server's settings are next loaded.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.ResourceServer,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        await store.AddAsync(resourceServer, cancellationToken);
        var creatorAssignment = await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            resourceServer.TenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServer.ResourceServerId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServer.ResourceServerId,
            ResourceChangeTypes.Created,
            ToResourceValues(ToResourceServerResource(resourceServer))
        );

        if (creatorAssignment is not null)
        {
            await RecordRoleAssignmentChangedAsync(
                httpContext,
                creatorAssignment,
                ResourceChangeTypes.Created
            );
        }

        httpContext.Response.Headers.ETag = resourceServer.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}",
            ToResourceServerResource(resourceServer)
        );
    }

    /// <summary>
    /// Updates a resource server (API).
    /// </summary>
    /// <response code="204">The resource server was updated.</response>
    [EndpointName("api/resource-servers/update")]
    internal virtual async ValueTask<IResult> UpdateAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] JsonPatchDocument<UpdateResourceServerRequest> request,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await ResourceServerValidator.AuthorizeUpdateAsync(
            httpContext.User,
            tenantId,
            resourceServerId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var error = await ResourceServerValidator.ValidateUpdateAsync(
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var model = new UpdateResourceServerRequest
        {
            Name = resourceServer.Name,
            IsDisabled = resourceServer.IsDisabled,
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

        resourceServer.Name = model.Name;
        resourceServer.IsDisabled = model.IsDisabled;

        await store.UpdateAsync(resourceServer, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServer.ResourceServerId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToResourceServerResource(resourceServer))
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a resource server (API).
    /// </summary>
    /// <response code="204">The resource server was deleted.</response>
    /// <response code="409">The resource server is a system server, is referenced by a client grant, or a client acts for it.</response>
    [EndpointName("api/resource-servers/delete")]
    internal virtual async ValueTask<IResult> DeleteAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var authorizationError = await ResourceServerValidator.AuthorizeDeleteAsync(
            httpContext.User,
            tenantId,
            resourceServerId,
            cancellationToken
        );
        if (authorizationError is not null)
        {
            return ToErrorResult(authorizationError);
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var error = await ResourceServerValidator.ValidateDeleteAsync(
            resourceServer,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        var resourceValues = ToResourceValues(ToResourceServerResource(resourceServer));

        await store.RemoveAsync(resourceServerId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServer.ResourceServerId,
            ResourceChangeTypes.Deleted,
            resourceValues
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a resource server's scopes.
    /// </summary>
    /// <response code="200">The resource server's scopes.</response>
    [EndpointName("api/resource-servers/scopes/list")]
    internal virtual async ValueTask<IResult> ListScopesAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();
                return await store.GetOrDefaultAsync(resourceServerId, token);
            },
            value => (IReadOnlyList<ScopeResource>)value.Scopes.Select(ToScopeResource).ToList(),
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a scope on a resource server.
    /// </summary>
    /// <response code="201">The created scope.</response>
    /// <response code="409">A scope with the same value already exists.</response>
    [EndpointName("api/resource-servers/scopes/create")]
    internal virtual async ValueTask<IResult> CreateScopeAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] CreateScopeRequest request,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        var persistedScope = new PersistedScope
        {
            Value = request.Value,
            Description = request.Description,
            IsSystem = false,
        };

        try
        {
            await store.AddScopeAsync(resourceServerId, persistedScope, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A scope with the specified value already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await ManagementAuditRecorder.RecordResourceServerScopeChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServerId,
            persistedScope.Value,
            ResourceChangeTypes.Created,
            ToResourceValues(ToScopeResource(persistedScope))
        );

        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/scopes/{request.Value}",
            ToScopeResource(persistedScope)
        );
    }

    /// <summary>
    /// Gets a scope on a resource server.
    /// </summary>
    /// <response code="200">The requested scope.</response>
    [EndpointName("api/resource-servers/scopes/get")]
    internal virtual async ValueTask<IResult> GetScopeAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();
                var resourceServer = await store.GetOrDefaultAsync(resourceServerId, token);
                return resourceServer?.Scopes.FirstOrDefault(scope =>
                    string.Equals(scope.Value, scopeValue, StringComparison.OrdinalIgnoreCase)
                );
            },
            ToScopeResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a scope on a resource server.
    /// </summary>
    /// <response code="204">The scope was updated.</response>
    [EndpointName("api/resource-servers/scopes/update")]
    internal virtual async ValueTask<IResult> UpdateScopeAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        [FromBody] UpdateScopeRequest request,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            await store.UpdateScopeAsync(
                resourceServerId,
                new PersistedScope
                {
                    Value = scopeValue,
                    Description = request.Description,
                    IsSystem = false,
                },
                cancellationToken
            );
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }

        await ManagementAuditRecorder.RecordResourceServerScopeChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServerId,
            scopeValue,
            ResourceChangeTypes.Updated,
            ToResourceValues(
                ToScopeResource(
                    new PersistedScope
                    {
                        Value = scopeValue,
                        Description = request.Description,
                        IsSystem = false,
                    }
                )
            )
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a scope on a resource server.
    /// </summary>
    /// <response code="204">The scope was deleted.</response>
    /// <response code="409">The scope is a system scope and cannot be removed.</response>
    [EndpointName("api/resource-servers/scopes/delete")]
    internal virtual async ValueTask<IResult> DeleteScopeAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string scopeValue,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        bool removed;
        try
        {
            removed = await store.RemoveScopeAsync(resourceServerId, scopeValue, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict
            );
        }

        if (!removed)
        {
            return TypedResults.NotFound();
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerScopeChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServerId,
            scopeValue,
            ResourceChangeTypes.Deleted,
            resourceValues: null
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a resource server's owners and whether the caller may grant and revoke the role.
    /// </summary>
    /// <response code="200">The resource server's owners and whether the caller may change them.</response>
    [EndpointName("api/resource-servers/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1465.
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );
        return await ProcessListRoleAssignmentsAsync(
            httpContext,
            resourceServerNode,
            BuiltInRoles.Owner,
            (storeManager, token) =>
                ResourceServerExistsAsync(storeManager, resourceServerId, token),
            (_, _) => HoldsAuthorityAsync(httpContext, resourceServerNode, Operations.Update),
            cancellationToken
        );
    }

    /// <summary>
    /// Adds an owner to a resource server.
    /// </summary>
    /// <response code="201">The owner was added.</response>
    /// <response code="200">The principal already owns the resource server.</response>
    [EndpointName("api/resource-servers/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] AddRoleHolderRequest request,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1469.
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );
        return await ProcessAddRoleAssignmentAsync(
            httpContext,
            resourceServerNode,
            BuiltInRoles.Owner,
            Operations.Update,
            request,
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/owners",
            (storeManager, token) =>
                ResourceServerExistsAsync(storeManager, resourceServerId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Removes an owner from a resource server.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
    [EndpointName("api/resource-servers/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );
        return await ProcessRemoveOwnerAsync(
            httpContext,
            resourceServerNode,
            principalId,
            cancellationToken
        );
    }

    private static async ValueTask<bool> ResourceServerExistsAsync(
        IStoreManager storeManager,
        string resourceServerId,
        CancellationToken cancellationToken
    ) =>
        await storeManager
            .GetStore<IResourceServerStore>()
            .GetOrDefaultAsync(resourceServerId, cancellationToken)
            is not null;

    // Settings and secrets

    internal virtual ResourceServerSettingsResource ToResourceServerSettingsResource(
        PersistedResourceServerSettings settings
    ) =>
        new()
        {
            TenantId = settings.TenantId,
            ResourceServerId = settings.ResourceServerId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };

    internal virtual ResourceServerSecretsResource ToResourceServerSecretsResource(
        PersistedResourceServerSecrets secrets
    ) =>
        new()
        {
            TenantId = secrets.TenantId,
            ResourceServerId = secrets.ResourceServerId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };

    internal virtual ResourceServerEffectiveSettingsResource ToResourceServerEffectiveSettingsResource(
        PersistedResourceServer resourceServer,
        IReadOnlySettingCollection settings
    ) =>
        new()
        {
            TenantId = resourceServer.TenantId,
            ResourceServerId = resourceServer.ResourceServerId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.ResourceServer
            ),
        };

    /// <summary>
    /// Merges a resource server's persisted settings onto the addressed tenant's effective settings, producing the same
    /// effective <see cref="IReadOnlySettingCollection"/> the authorization server resolves at runtime (the team's
    /// effective settings merged with the tenant's — expanded with the ceilings of the security profiles it selects —
    /// and then the resource server's persisted overrides). Every document is read through the <see
    /// cref="IEffectiveSettingsResolver"/>; a resource server selects no security profile, so its document adds none. A
    /// document that cannot be honored fails naming its owner.
    /// </summary>
    /// <exception cref="UnreadableSettingsException">The resource server's document, the tenant's or its team's
    /// cannot be honored.</exception>
    // See ADR-1149, ADR-1204 and ADR-1245.
    internal virtual async ValueTask<IReadOnlySettingCollection?> MergeResourceServerSettingsAsync(
        string tenantId,
        PersistedResourceServer resourceServer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenant = await storeManager
            .GetStore<ITenantStore>()
            .GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var teamEffective = await EffectiveSettingsResolver.ResolveTeamAsync(
            tenant.TeamId,
            cancellationToken
        );
        var tenantEffective = EffectiveSettingsResolver.ResolveStored(
            teamEffective,
            OpenIdSettingScopes.Tenant,
            tenantId,
            tenant.Settings.Value
        );
        return EffectiveSettingsResolver.ResolveStored(
            tenantEffective,
            OpenIdSettingScopes.ResourceServer,
            resourceServer.ResourceServerId,
            resourceServer.Settings.Value
        );
    }

    /// <summary>
    /// Gets a resource server's persisted settings.
    /// </summary>
    /// <response code="200">The resource server's settings.</response>
    [EndpointName("api/resource-servers/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();
                var resourceServer = await store.GetOrDefaultAsync(resourceServerId, token);
                return resourceServer?.Settings;
            },
            ToResourceServerSettingsResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the limits a resource server's settings carry and whether the caller may set them: whether it holds update
    /// authority over the resource server's tenant.
    /// </summary>
    /// <response code="200">The resource server's setting limits.</response>
    // See ADR-1448.
    [EndpointName("api/resource-servers/settings/limits/get")]
    internal virtual async ValueTask<IResult> GetSettingLimitsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        async ValueTask<ResourceNode?> ReadNodeAboveAsync(CancellationToken token)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(token);
            var store = storeManager.GetStore<IResourceServerStore>();
            var resourceServer = await store.GetOrDefaultAsync(resourceServerId, token);
            return resourceServer is null
                ? null
                : ResourceNode.For(
                    resourceServer.TenantId,
                    ResourceNodeTypes.Tenant,
                    resourceServer.TenantId
                );
        }

        var limitNames = SettingSerializer.GetLimits(OpenIdSettingScopes.ResourceServer);
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        return await ProcessGetSettingLimitsAsync(
            httpContext,
            resourceServerNode,
            limitNames,
            ReadNodeAboveAsync,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates a resource server's persisted settings via a JSON Patch document.
    /// </summary>
    /// <response code="204">The settings were updated.</response>
    /// <response code="412">The supplied <c>If-Match</c> concurrency token did not match.</response>
    [EndpointName("api/resource-servers/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        var settings = resourceServer?.Settings;
        if (settings is null)
        {
            return TypedResults.NotFound();
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, settings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(settings.Value);

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
            OpenIdSettingScopes.ResourceServer,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var changedLimits = SettingSerializer.GetChangedLimits(settings.Value, settingsValue);
        var authorityAbove = ResourceNode.For(
            settings.TenantId,
            ResourceNodeTypes.Tenant,
            settings.TenantId
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

        settings.Value = settingsValue;
        await store.UpdateSettingsAsync(settings, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerChangedAsync(
            httpContext,
            settings.TenantId,
            settings.ResourceServerId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToResourceServerSettingsResource(settings))
        );

        httpContext.Response.Headers.ETag = settings.ConcurrencyToken;
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a resource server's effective (merged) settings.
    /// </summary>
    /// <response code="200">The resource server's effective settings.</response>
    [EndpointName("api/resource-servers/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // A null read is the absent resource server or the absent tenant, answered 404. See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();

                var resourceServer = await store.GetOrDefaultAsync(resourceServerId, token);
                if (resourceServer is null)
                {
                    return null;
                }

                var effectiveSettings = await MergeResourceServerSettingsAsync(
                    tenantId,
                    resourceServer,
                    storeManager,
                    token
                );
                return effectiveSettings is null
                    ? null
                    : ToResourceServerEffectiveSettingsResource(resourceServer, effectiveSettings);
            },
            resource => resource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the collection of secrets owned by a resource server.
    /// </summary>
    /// <response code="200">The resource server's secrets.</response>
    [EndpointName("api/resource-servers/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();
                return await store.GetSecretsOrDefaultAsync(resourceServerId, token);
            },
            ToResourceServerSecretsResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates a secret for a resource server. The generated key material is returned exactly once.
    /// </summary>
    /// <response code="201">The created secret, including its one-time key material.</response>
    /// <response code="409">The resource server is a system resource server, which holds no keys.</response>
    [EndpointName("api/resource-servers/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Create,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        if (resourceServer.IsSystem)
        {
            return SystemResourceServerHoldsNoKeys();
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validation = SecretValidator.ValidateCreate(request, createdWhen);
        if (!validation.IsValid)
        {
            return ToErrorResult(validation.Error);
        }

        var secretId = CryptoService.GenerateResourceId();

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(resourceServer.TenantId),
            [],
            cancellationToken
        );

        GeneratedSecret generatedSecret;
        try
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                protector,
                new GenerateSecretRequest
                {
                    SecretId = secretId,
                    SecretType = request.SecretType,
                    GenerationParameters = validation.GenerationParameters,
                    Use = request.Use,
                    Algorithm = request.Algorithm,
                    CreatedWhen = createdWhen,
                    ActivatesWhen = request.ActivatesWhen,
                    RetiresWhen = request.RetiresWhen,
                    ExpiresWhen = request.ExpiresWhen,
                }
            );
        }
        catch (Exception exception)
            when (exception is ArgumentException or InvalidOperationException)
        {
            Logger.SecretGenerationFailed(exception);
            return TypedResults.Problem(
                detail: "The secret could not be generated from the supplied parameters.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        try
        {
            await store.AddSecretAsync(resourceServerId, generatedSecret.Secret, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A secret with the specified identifier already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerSecretChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServerId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(ToSecretResource(generatedSecret.Secret))
        );

        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}",
            ToCreatedSecretResource(generatedSecret.Secret, generatedSecret.SecretMaterial)
        );
    }

    /// <summary>
    /// Imports a caller-supplied public key (JWK, PEM, or certificate) as a new resource-server secret.
    /// </summary>
    /// <response code="201">The imported secret.</response>
    /// <response code="409">The resource server is a system resource server, which holds no keys.</response>
    // See ADR-1104.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromBody] ImportSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Create,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServer = await store.GetOrDefaultAsync(resourceServerId, cancellationToken);
        if (resourceServer is null)
        {
            return TypedResults.NotFound();
        }

        if (resourceServer.IsSystem)
        {
            return SystemResourceServerHoldsNoKeys();
        }

        var createdWhen = TimeProvider.GetUtcNow();
        var validationError = SecretValidator.ValidateImport(request, createdWhen);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        var secretId = CryptoService.GenerateResourceId();

        var importRequest = new SecretKeyImportRequest
        {
            SecretId = secretId,
            SecretType = request.SecretType,
            Format = request.Format,
            KeyMaterial = request.KeyMaterial,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = createdWhen,
            ActivatesWhen = request.ActivatesWhen,
            RetiresWhen = request.RetiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };
        if (!KeyImporter.TryImportKey(importRequest, out var importedSecret, out var importError))
        {
            return TypedResults.Problem(
                detail: importError,
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var keyIdConflict = KeyIdConflictOrDefault(
            resourceServer.Secrets.Value,
            importedSecret,
            "resource server"
        );
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        try
        {
            await store.AddSecretAsync(resourceServerId, importedSecret, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            Logger.ResourceConflict(exception);
            return TypedResults.Problem(
                detail: "A secret with the specified identifier already exists.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await storeManager.SaveChangesAsync(cancellationToken);

        var importedSecretResource = ToSecretResource(importedSecret);

        await ManagementAuditRecorder.RecordResourceServerSecretChangedAsync(
            httpContext,
            resourceServer.TenantId,
            resourceServerId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(importedSecretResource)
        );

        httpContext.Response.Headers.ETag = importedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets/{secretId}",
            importedSecretResource
        );
    }

    /// <summary>
    /// Gets a single secret owned by a resource server.
    /// </summary>
    /// <response code="200">The requested secret.</response>
    [EndpointName("api/resource-servers/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();

                var resourceServerSecret = await store.GetSecretOrDefaultAsync(
                    resourceServerId,
                    secretId,
                    token
                );
                return resourceServerSecret?.Value;
            },
            ToSecretResource,
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the public key for a resource server's secret, or <c>null</c> for a symmetric secret.
    /// </summary>
    /// <response code="200">The requested public key.</response>
    [EndpointName("api/resource-servers/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.ResourceServer, resourceServerId);
        return await ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            async token =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(token);
                var store = storeManager.GetStore<IResourceServerStore>();

                var resourceServerSecret = await store.GetSecretOrDefaultAsync(
                    resourceServerId,
                    secretId,
                    token
                );
                return resourceServerSecret?.Value;
            },
            // The public key is read from its stored public half, so no master key is loaded (ADR-1268).
            SecretPublicKeyConverter.Convert,
            cancellationToken
        );
    }

    /// <summary>
    /// Updates the metadata of a resource server's secret. Key material is immutable.
    /// </summary>
    /// <response code="204">The secret was updated.</response>
    /// <response code="412">The supplied <c>If-Match</c> concurrency token did not match.</response>
    [EndpointName("api/resource-servers/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Update,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );
        if (resourceServerSecret is null)
        {
            return TypedResults.NotFound();
        }

        var secret = resourceServerSecret.Value;

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, secret.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var metadataError = SecretValidator.ValidateUpdate(secret, request);
        if (metadataError is not null)
        {
            return ToErrorResult(metadataError);
        }

        var updated = WithUpdate(secret, request);

        await store.UpdateSecretAsync(resourceServerId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerSecretChangedAsync(
            httpContext,
            resourceServerSecret.TenantId,
            resourceServerId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated))
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a resource server's secret.
    /// </summary>
    /// <response code="204">The secret was deleted.</response>
    [EndpointName("api/resource-servers/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string resourceServerId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        var resourceServerNode = ResourceNode.For(
            tenantId,
            ResourceNodeTypes.ResourceServer,
            resourceServerId
        );

        var authorizationFailure = await GetAuthorizationFailureAsync(
            httpContext,
            resourceServerNode,
            Operations.Delete,
            cancellationToken
        );
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IResourceServerStore>();

        var resourceServerSecret = await store.GetSecretOrDefaultAsync(
            resourceServerId,
            secretId,
            cancellationToken
        );
        if (resourceServerSecret is null)
        {
            return TypedResults.NotFound();
        }

        await store.RemoveSecretAsync(resourceServerId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordResourceServerSecretChangedAsync(
            httpContext,
            resourceServerSecret.TenantId,
            resourceServerId,
            secretId,
            ResourceChangeTypes.Deleted,
            ToResourceValues(ToSecretResource(resourceServerSecret.Value))
        );

        return TypedResults.NoContent();
    }
}
