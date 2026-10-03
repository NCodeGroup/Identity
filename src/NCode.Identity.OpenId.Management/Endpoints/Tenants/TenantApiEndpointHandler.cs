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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NCode.Identity.Endpoints;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/*

GET   api/tenants/{tenantId}

GET   api/tenants/{tenantId}/settings
PATCH api/tenants/{tenantId}/settings

GET    api/tenants/{tenantId}/secrets
POST   api/tenants/{tenantId}/secrets
GET    api/tenants/{tenantId}/secrets/{secretId}
PUT    api/tenants/{tenantId}/secrets/{secretId}
DELETE api/tenants/{tenantId}/secrets/{secretId}

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Tenant, including its settings and secrets.
/// </summary>
internal class TenantApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ITenantValidator tenantValidator,
    ISecretGenerator secretGenerator,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IResourceOwnershipService resourceOwnershipService,
    ILogger<TenantApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IManagementEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ITenantValidator TenantValidator { get; } = tenantValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<TenantApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/tenants").WithTags("Tenants");

        tenants
            .MapPost("", CreateTenantAsync)
            .Produces<TenantResource>(StatusCodes.Status201Created);
        tenants.MapGet("", ListTenantsAsync).Produces<CollectionResource<TenantResource>>();
        tenants.MapGet("/{tenantId}", GetTenantAsync).Produces<TenantResource>();
        tenants.MapPatch("/{tenantId}", UpdateTenantAsync);
        tenants.MapDelete("/{tenantId}", DeleteTenantAsync);

        tenants.MapGet("/{tenantId}/settings", GetSettingsAsync).Produces<TenantSettingsResource>();
        tenants.MapPatch("/{tenantId}/settings", UpdateSettingsAsync);

        tenants.MapGet("/{tenantId}/secrets", GetSecretsAsync).Produces<TenantSecretsResource>();
        tenants
            .MapPost("/{tenantId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        tenants.MapGet("/{tenantId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        tenants.MapPut("/{tenantId}/secrets/{secretId}", UpdateSecretAsync);
        tenants.MapDelete("/{tenantId}/secrets/{secretId}", DeleteSecretAsync);

        tenants
            .MapGet("/{tenantId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        tenants
            .MapPost("/{tenantId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        tenants.MapDelete("/{tenantId}/owners/{principalId}", RemoveOwnerAsync);
    }

    /// <summary>
    /// Maps a <see cref="PersistedTenant"/> to its <see cref="TenantResource"/> representation.
    /// </summary>
    /// <param name="tenant">The <see cref="PersistedTenant"/> to map.</param>
    /// <returns>The mapped <see cref="TenantResource"/>.</returns>
    internal virtual TenantResource ToTenantResource(PersistedTenant tenant)
    {
        return new TenantResource
        {
            TenantId = tenant.TenantId,
            ConcurrencyToken = tenant.ConcurrencyToken,
            DomainName = tenant.DomainName,
            IsDisabled = tenant.IsDisabled,
            DisplayName = tenant.DisplayName,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedTenantSettings"/> to its <see cref="TenantSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedTenantSettings"/> to map.</param>
    /// <returns>The mapped <see cref="TenantSettingsResource"/>.</returns>
    internal virtual TenantSettingsResource ToTenantSettingsResource(
        PersistedTenantSettings settings
    )
    {
        return new TenantSettingsResource
        {
            TenantId = settings.TenantId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedTenantSecrets"/> to its <see cref="TenantSecretsResource"/> representation.
    /// </summary>
    /// <param name="secrets">The <see cref="PersistedTenantSecrets"/> to map.</param>
    /// <returns>The mapped <see cref="TenantSecretsResource"/>.</returns>
    internal virtual TenantSecretsResource ToTenantSecretsResource(PersistedTenantSecrets secrets)
    {
        return new TenantSecretsResource
        {
            TenantId = secrets.TenantId,
            ConcurrencyToken = secrets.ConcurrencyToken,
            Secrets = ToSecretsResource(secrets.Value),
        };
    }

    /// <summary>
    /// Handles <c>GET api/tenants</c>, returning a page of OpenID Tenants.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of tenants to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/list")]
    internal virtual async ValueTask<IResult> ListTenantsAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        // Tenant administration is central-admin; a null authorization resource restricts this to global admins.
        return await ProcessListAsync(
            httpContext,
            authorizationResource: null,
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<ITenantStore>();
                return await store.GetPageAsync(cursor, effectiveLimit, cancellationToken);
            },
            ToTenantResource
        );
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}</c>, returning the specified OpenID Tenant resource.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/get")]
    internal virtual async ValueTask<IResult> GetTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(httpContext, tenant, node, Operations.Read, ToTenantResource);
    }

    /// <summary>
    /// Handles <c>POST api/tenants</c>, creating a new OpenID Tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreateTenantRequest"/> describing the tenant to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/create")]
    internal virtual async ValueTask<IResult> CreateTenantAsync(
        HttpContext httpContext,
        OpenIdContext openIdContext,
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantId = CryptoService.GenerateResourceId();

        var tenant = new PersistedTenant
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            DomainName = request.DomainName,
            IsDisabled = request.IsDisabled,
            DisplayName = request.DisplayName,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        // Core preconditions (authorization + uniqueness) are asserted by the validator (ADR-0014).
        var error = await TenantValidator.ValidateCreateAsync(
            httpContext.User,
            tenant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await store.AddAsync(tenant, cancellationToken);
        await ResourceOwnershipService.AssignCreatorAsync(
            openIdContext,
            httpContext.User,
            storeManager,
            tenant.TenantId,
            ResourceNodeTypes.Tenant,
            tenant.TenantId,
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        // The store assigns the row token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = tenant.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenant.TenantId}", ToTenantResource(tenant));
    }

    /// <summary>
    /// Handles <c>PATCH api/tenants/{tenantId}</c>, applying a JSON Patch document to a tenant's metadata.
    /// Honors an <c>If-Match</c> precondition.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the tenant metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/update")]
    internal virtual async ValueTask<IResult> UpdateTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] JsonPatchDocument<UpdateTenantRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdateTenantRequest
        {
            DomainName = tenant.DomainName,
            DisplayName = tenant.DisplayName,
            IsDisabled = tenant.IsDisabled,
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

        // Core preconditions (authorization + If-Match + required fields + domain uniqueness) are asserted by the
        // validator against the hydrated model (ADR-0014).
        var error = await TenantValidator.ValidateUpdateAsync(
            httpContext.User,
            tenant,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        tenant.DomainName = model.DomainName;
        tenant.IsDisabled = model.IsDisabled;
        tenant.DisplayName = model.DisplayName;

        await store.UpdateAsync(tenant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/tenants/{tenantId}</c>, removing a tenant. The removal is rejected with
    /// <c>409 Conflict</c> while the tenant still has clients or secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/delete")]
    internal virtual async ValueTask<IResult> DeleteTenantAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        // Core preconditions (authorization + no-dependents) are asserted by the validator (ADR-0014).
        var error = await TenantValidator.ValidateDeleteAsync(
            httpContext.User,
            tenant,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        await store.RemoveAsync(tenantId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/settings</c>, returning the settings for the specified OpenID Tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSettings = await store.GetSettingsOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            tenantSettings,
            node,
            Operations.Read,
            ToTenantSettingsResource
        );
    }

    /// <summary>
    /// Handles <c>PATCH api/tenants/{tenantId}/settings</c>, applying a JSON Patch document to the settings for the
    /// specified OpenID Tenant. Honors an <c>If-Match</c> precondition and returns the refreshed <c>ETag</c>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSettings = await store.GetSettingsOrDefaultAsync(tenantId, cancellationToken);

        if (tenantSettings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenantSettings,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, tenantSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(tenantSettings.Value);

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

        tenantSettings.Value = SerializeToElement(jsonObject);

        await store.UpdateSettingsAsync(tenantSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ETag = tenantSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/secrets</c>, returning the secrets for the specified OpenID Tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/secrets/get")]
    internal virtual async ValueTask<IResult> GetSecretsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantSecrets = await store.GetSecretsOrDefaultAsync(tenantId, cancellationToken);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            tenantSecrets,
            node,
            Operations.Read,
            ToTenantSecretsResource
        );
    }

    /// <summary>
    /// Handles <c>POST api/tenants/{tenantId}/secrets</c>, generating a new server-side secret and persisting it.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/secrets/create")]
    internal virtual async ValueTask<IResult> CreateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] CreateSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            tenant,
            Operations.Create
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var secretId = CryptoService.GenerateResourceId();

        PersistedSecret generatedSecret;
        try
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                new GenerateSecretRequest
                {
                    SecretId = secretId,
                    SecretType = request.SecretType,
                    KeySizeBits = request.KeySizeBits,
                    Use = request.Use,
                    Algorithm = request.Algorithm,
                    CreatedWhen = TimeProvider.GetUtcNow(),
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
            await store.AddSecretAsync(tenantId, generatedSecret, cancellationToken);
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

        // The store assigns the secret's token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/secrets/{secretId}",
            ToSecretResource(generatedSecret)
        );
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/secrets/{secretId}</c>, returning a single tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/secrets/get-one")]
    internal virtual async ValueTask<IResult> GetSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        var scoped = TenantOwnedResource.ForOrDefault(tenantId, secret);

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            scoped,
            node,
            Operations.Read,
            x => ToSecretResource(x.Value)
        );
    }

    /// <summary>
    /// Handles <c>PUT api/tenants/{tenantId}/secrets/{secretId}</c>, updating a secret's metadata. Key material
    /// is immutable. Honors an <c>If-Match</c> precondition.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/secrets/update")]
    internal virtual async ValueTask<IResult> UpdateSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        [FromBody] UpdateSecretRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(tenantId, secret),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, secret.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var updated = new PersistedSecret
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        };

        await store.UpdateSecretAsync(tenantId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>DELETE api/tenants/{tenantId}/secrets/{secretId}</c>, removing a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    [EndpointName("api/tenants/secrets/delete")]
    internal virtual async ValueTask<IResult> DeleteSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string secretId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var secret = await store.GetSecretOrDefaultAsync(tenantId, secretId, cancellationToken);
        if (secret is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            TenantOwnedResource.For(tenantId, secret),
            Operations.Delete
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        await store.RemoveSecretAsync(tenantId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Handles <c>GET api/tenants/{tenantId}/owners</c>, returning the tenant's owners.
    /// </summary>
    [EndpointName("api/tenants/owners/list")]
    internal virtual async ValueTask<IResult> ListOwnersAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessListOwnersAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            (storeManager, token) => TenantExistsAsync(storeManager, tenantId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Handles <c>POST api/tenants/{tenantId}/owners</c>, granting a principal ownership of the tenant.
    /// </summary>
    [EndpointName("api/tenants/owners/add")]
    internal virtual async ValueTask<IResult> AddOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] AddOwnerRequest request,
        CancellationToken cancellationToken
    )
    {
        return await ProcessAddOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            request,
            $"/tenants/{tenantId}/owners",
            (storeManager, token) => TenantExistsAsync(storeManager, tenantId, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Handles <c>DELETE api/tenants/{tenantId}/owners/{principalId}</c>, revoking a principal's ownership of the
    /// tenant. Refused when it would leave the tenant with no owner.
    /// </summary>
    [EndpointName("api/tenants/owners/remove")]
    internal virtual async ValueTask<IResult> RemoveOwnerAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromRoute] string principalId,
        CancellationToken cancellationToken
    )
    {
        return await ProcessRemoveOwnerAsync(
            httpContext,
            tenantId,
            ResourceNodeTypes.Tenant,
            tenantId,
            principalId,
            cancellationToken
        );
    }

    private static async ValueTask<bool> TenantExistsAsync(
        IStoreManager storeManager,
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        await storeManager.GetStore<ITenantStore>().GetOrDefaultAsync(tenantId, cancellationToken)
            is not null;
}
