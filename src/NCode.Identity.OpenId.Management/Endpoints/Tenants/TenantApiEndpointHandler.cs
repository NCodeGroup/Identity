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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/*

GET   api/tenants/{tenantId}
GET   api/tenants/{tenantId}/address

GET   api/tenants/{tenantId}/settings
PATCH api/tenants/{tenantId}/settings
GET   api/tenants/{tenantId}/effective-settings

GET    api/tenants/{tenantId}/secrets
POST   api/tenants/{tenantId}/secrets
GET    api/tenants/{tenantId}/secrets/{secretId}
GET    api/tenants/{tenantId}/secrets/{secretId}/public-key
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
    ITenantResolver tenantResolver,
    ISecretValidator secretValidator,
    ISecretGenerator secretGenerator,
    IClientKeyImporter keyImporter,
    ISecretPublicKeyConverter secretPublicKeyConverter,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IResourceOwnershipService resourceOwnershipService,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<TenantApiEndpointHandler> logger
) : BaseOwnableApiEndpointHandler, IEndpointProvider
{
    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ITenantValidator TenantValidator { get; } = tenantValidator;
    private ITenantResolver TenantResolver { get; } = tenantResolver;
    private ISecretValidator SecretValidator { get; } = secretValidator;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter KeyImporter { get; } = keyImporter;
    private ISecretPublicKeyConverter SecretPublicKeyConverter { get; } = secretPublicKeyConverter;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;

    /// <inheritdoc />
    protected override IResourceOwnershipService ResourceOwnershipService { get; } =
        resourceOwnershipService;
    private ILogger<TenantApiEndpointHandler> Logger { get; } = logger;

    /// <summary>
    /// Gets the most days one read of a tenant's usage spans: a year of the ledger's retention (ADR-0199).
    /// </summary>
    internal const int MaxUsageDays = 366;

    /// <summary>
    /// Gets how many days before the last day a read of a tenant's usage starts when it names no first day.
    /// </summary>
    internal const int DefaultUsageDays = 30;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var tenants = endpoints.MapGroup("/tenants").WithTags(OpenIdConstants.EndpointTags.Tenants);

        tenants
            .MapPost("", CreateTenantAsync)
            .Produces<TenantResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        tenants.MapGet("", ListTenantsAsync).Produces<CollectionResource<TenantResource>>();
        tenants.MapGet("/{tenantId}", GetTenantAsync).Produces<TenantResource>();
        tenants.MapPatch("/{tenantId}", UpdateTenantAsync).Produces(StatusCodes.Status204NoContent);
        tenants
            .MapDelete("/{tenantId}", DeleteTenantAsync)
            .Produces(StatusCodes.Status204NoContent);
        tenants.MapGet("/{tenantId}/address", GetAddressAsync).Produces<TenantAddressResource>();
        tenants
            .MapGet("/{tenantId}/usage", GetUsageAsync)
            .Produces<TenantUsageResource>()
            .ProducesValidationProblem();

        tenants.MapGet("/{tenantId}/settings", GetSettingsAsync).Produces<TenantSettingsResource>();
        tenants
            .MapPatch("/{tenantId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        tenants
            .MapGet("/{tenantId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<TenantEffectiveSettingsResource>();

        tenants.MapGet("/{tenantId}/secrets", GetSecretsAsync).Produces<TenantSecretsResource>();
        tenants
            .MapPost("/{tenantId}/secrets", CreateSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        tenants
            .MapPost("/{tenantId}/secrets/import", ImportSecretAsync)
            .Produces<SecretResource>(StatusCodes.Status201Created);
        tenants.MapGet("/{tenantId}/secrets/{secretId}", GetSecretAsync).Produces<SecretResource>();
        tenants
            .MapGet("/{tenantId}/secrets/{secretId}/public-key", GetPublicKeyAsync)
            .Produces<PublicKeyResource>();
        tenants
            .MapPut("/{tenantId}/secrets/{secretId}", UpdateSecretAsync)
            .Produces(StatusCodes.Status204NoContent);
        tenants
            .MapDelete("/{tenantId}/secrets/{secretId}", DeleteSecretAsync)
            .Produces(StatusCodes.Status204NoContent);

        tenants
            .MapGet("/{tenantId}/owners", ListOwnersAsync)
            .Produces<CollectionResource<OwnerResource>>();
        tenants
            .MapPost("/{tenantId}/owners", AddOwnerAsync)
            .Produces<OwnerResource>(StatusCodes.Status201Created);
        tenants
            .MapDelete("/{tenantId}/owners/{principalId}", RemoveOwnerAsync)
            .Produces(StatusCodes.Status204NoContent);
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
            Issuer = tenant.Issuer,
            MtlsEndpointAliasAuthority = tenant.MtlsEndpointAliasAuthority,
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
    /// Maps a tenant's effective (merged) <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="TenantEffectiveSettingsResource"/> representation: a flat JSON object of setting name to formatted
    /// value, using the same representation as the discovery document, ordered by name for a stable dump.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the settings.</param>
    /// <param name="settings">The effective <see cref="IReadOnlySettingCollection"/> to map.</param>
    /// <returns>The mapped <see cref="TenantEffectiveSettingsResource"/>.</returns>
    internal virtual TenantEffectiveSettingsResource ToTenantEffectiveSettingsResource(
        string tenantId,
        IReadOnlySettingCollection settings
    )
    {
        return new TenantEffectiveSettingsResource
        {
            TenantId = tenantId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Tenant
            ),
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
    /// Lists the OpenID tenants.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of tenants to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of tenants.</response>
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
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToTenantResource
        );
    }

    /// <summary>
    /// Gets an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested tenant.</response>
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
    /// Creates an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreateTenantRequest"/> describing the tenant to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created tenant.</response>
    [EndpointName("api/tenants/create")]
    internal virtual async ValueTask<IResult> CreateTenantAsync(
        HttpContext httpContext,
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        // A tenant is named by its creator (ADR-0159); the validator checks the name with the rest of its address.
        var tenantId = request.TenantId;

        // A tenant written without an issuer gets the one its strategy derives for it, judged like an explicit one
        // (ADR-0165).
        var issuer =
            NullIfEmpty(request.Issuer)
            ?? await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);
        var mtlsEndpointAliasAuthority =
            NullIfEmpty(request.MtlsEndpointAliasAuthority)
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenantId,
                cancellationToken
            );

        var tenant = new PersistedTenant
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            Issuer = issuer,
            MtlsEndpointAliasAuthority = mtlsEndpointAliasAuthority,
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

        // Core preconditions (authorization + the tenant's address + uniqueness) are asserted by the validator
        // (ADR-0014, ADR-0160).
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

        // Setting names are a closed vocabulary (ADR-0139), and a tenant carries only the settings that apply within it
        // (ADR-0149): a settings document that cannot be honored is rejected here rather than stored and failed on every
        // request that resolves the tenant.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Tenant,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        await store.AddAsync(tenant, cancellationToken);

        // The tenant row commits first: the creator's ownership is a row the tenant owns, and the stores resolve an
        // owning tenant from the database rather than the change tracker — the same reason the provisioner commits a
        // tenant before seeding it.
        await storeManager.SaveChangesAsync(cancellationToken);

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

        // The tenant is seeded as soon as it exists — after the root tenant — so its system resource servers and keys
        // are in place before its first request, through the one pipeline every tenant is seeded by (ADR-0166). The
        // provisioner is scoped (its mediator is request-scoped), so it is resolved from the request's services.
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();
        await provisioner.SeedAsync(tenant.TenantId, cancellationToken);

        // A seed contributor may write to the tenant row (a seeded signing key changes its secrets), which replaces its
        // concurrency token, so the response reflects the tenant as it now stands.
        tenant = await ReloadTenantAsync(tenant, cancellationToken);

        var resource = ToTenantResource(tenant);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource),
            cancellationToken
        );

        httpContext.Response.Headers.ETag = tenant.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenant.TenantId}", resource);
    }

    /// <summary>
    /// Previews the address of an OpenID tenant: the issuer and mutual-TLS endpoint-alias authority it has, or would have,
    /// and every reason it could not be served there. A caller checks a tenant's name and addresses with it before creating
    /// the tenant or changing them; the same rules judge the write itself.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant, whether or not it exists.</param>
    /// <param name="issuer">The proposed issuer; when omitted, the tenant's current issuer, or for a tenant without one
    /// the issuer its strategy derives for it.</param>
    /// <param name="mtlsEndpointAliasAuthority">The proposed mutual-TLS endpoint-alias authority; when omitted, the
    /// tenant's current alias, or for a tenant without one the alias its strategy derives for it.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's address and the reasons it cannot be served there, if any.</response>
    // See ADR-0165 (the address is previewed through the seam that judges it).
    [EndpointName("api/tenants/address")]
    internal virtual async ValueTask<IResult> GetAddressAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] string? issuer,
        [FromQuery] string? mtlsEndpointAliasAuthority,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        var exists = tenant is not null;

        // Previewing an existing tenant's address reads it; previewing a new one is part of creating it, which is
        // authority at the parent node rather than ownership (ADR-0034).
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
            exists ? Operations.Read : Operations.Create
        );
        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var proposedIssuer = NullIfEmpty(issuer) ?? NullIfEmpty(tenant?.Issuer);
        var effectiveIssuer =
            proposedIssuer
            ?? await TenantResolver.GetDefaultIssuerAsync(tenantId, cancellationToken);

        var proposedAlias =
            NullIfEmpty(mtlsEndpointAliasAuthority)
            ?? NullIfEmpty(tenant?.MtlsEndpointAliasAuthority);
        var effectiveAlias =
            proposedAlias
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenantId,
                cancellationToken
            );

        var failures = await TenantResolver.ValidateTenantAsync(
            tenantId,
            effectiveIssuer,
            effectiveAlias,
            storeManager,
            cancellationToken
        );

        return TypedResults.Ok(
            new TenantAddressResource
            {
                TenantId = tenantId,
                Issuer = effectiveIssuer,
                IsIssuerDerived = proposedIssuer is null && effectiveIssuer is not null,
                MtlsEndpointAliasAuthority = effectiveAlias,
                IsMtlsEndpointAliasAuthorityDerived =
                    proposedAlias is null && effectiveAlias is not null,
                Exists = exists,
                Failures = failures
                    .Select(failure => new TenantAddressFailure
                    {
                        Detail = failure.Detail,
                        IsConflict = failure.IsConflict,
                    })
                    .ToList(),
            }
        );
    }

    /// <summary>
    /// Updates an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the tenant metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant was updated.</response>
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
            Issuer = tenant.Issuer,
            MtlsEndpointAliasAuthority = tenant.MtlsEndpointAliasAuthority,
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

        // An update that clears the issuer or the alias restores the one the tenant's strategy derives for it (ADR-0165,
        // ADR-0195).
        model.Issuer =
            NullIfEmpty(model.Issuer)
            ?? await TenantResolver.GetDefaultIssuerAsync(tenant.TenantId, cancellationToken);
        model.MtlsEndpointAliasAuthority =
            NullIfEmpty(model.MtlsEndpointAliasAuthority)
            ?? await TenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync(
                tenant.TenantId,
                cancellationToken
            );

        // Core preconditions (authorization + If-Match + required fields + the tenant's address + issuer uniqueness)
        // are asserted by the validator against the hydrated model (ADR-0014, ADR-0160).
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

        tenant.Issuer = model.Issuer;
        tenant.MtlsEndpointAliasAuthority = model.MtlsEndpointAliasAuthority;
        tenant.IsDisabled = model.IsDisabled;
        tenant.DisplayName = model.DisplayName;

        await store.UpdateAsync(tenant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToTenantResource(tenant)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes an OpenID tenant.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant was deleted.</response>
    /// <response code="409">The tenant still has clients or resource servers and cannot be deleted.</response>
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

        var resourceValues = ToResourceValues(ToTenantResource(tenant));

        await store.RemoveAsync(tenantId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            ResourceChangeTypes.Deleted,
            resourceValues,
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenant.TenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a tenant's usage from the durable daily ledger: the count of each meter on each UTC calendar day of a range,
    /// which the platform bills a plan by and reports to the customer. The ledger keeps a deleted tenant's usage, so it
    /// can still be read.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="from">The first UTC calendar day, inclusive; when omitted, 30 days before <paramref name="to"/>.</param>
    /// <param name="to">The last UTC calendar day, inclusive; when omitted, the current UTC day.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's usage on the range.</response>
    /// <response code="400">The range is reversed, longer than <see cref="MaxUsageDays"/> days, or would start before the
    /// first representable day.</response>
    // See ADR-0199.
    [EndpointName("api/tenants/usage/get")]
    internal virtual async ValueTask<IResult> GetUsageAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken
    )
    {
        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            node,
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
        var usage = await store.ListAsync(tenantId, firstDay, lastDay, cancellationToken);

        return TypedResults.Ok(
            new TenantUsageResource
            {
                TenantId = tenantId,
                From = firstDay,
                To = lastDay,
                Entries = usage
                    .Select(entry => new TenantUsageEntry
                    {
                        Day = entry.Day,
                        Meter = entry.Meter,
                        Count = entry.Count,
                    })
                    .ToList(),
            }
        );
    }

    /// <summary>
    /// Gets a tenant's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's settings.</response>
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
    /// Gets a tenant's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's effective settings.</response>
    [EndpointName("api/tenants/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
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

        var effectiveSettings = await MergeTenantSettingsAsync(
            tenantSettings.Value,
            cancellationToken
        );

        var node = ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);
        return await ProcessGetAsync(
            httpContext,
            effectiveSettings,
            node,
            Operations.Read,
            settings => ToTenantEffectiveSettingsResource(tenantId, settings)
        );
    }

    /// <summary>
    /// Merges a tenant's persisted settings onto the server's effective settings through the
    /// <see cref="IEffectiveSettingsResolver"/>, producing the same effective <see cref="IReadOnlySettingCollection"/>
    /// the authorization server resolves for the tenant at runtime — including the ceilings of the security profiles the
    /// tenant selects (ADR-0204).
    /// </summary>
    /// <param name="tenantSettings">The tenant's settings document to merge.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective <see cref="IReadOnlySettingCollection"/>.</returns>
    internal virtual async ValueTask<IReadOnlySettingCollection> MergeTenantSettingsAsync(
        JsonElement tenantSettings,
        CancellationToken cancellationToken
    )
    {
        var serverEffective = await EffectiveSettingsResolver.ResolveServerAsync(cancellationToken);
        return EffectiveSettingsResolver.Resolve(
            serverEffective,
            OpenIdSettingScopes.Tenant,
            tenantSettings
        );
    }

    /// <summary>
    /// Verifies that replacing a tenant's settings document leaves every value each of its clients registers permitted by
    /// every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The clients are not walked when the
    /// change leaves the effective value of every setting a rule reads unchanged.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant whose settings change.</param>
    /// <param name="currentSettings">The tenant's current settings document.</param>
    /// <param name="newSettings">The tenant's new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenant's clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the tenant setting that does not permit each value, or
    /// an empty list.</returns>
    // See ADR-0206.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        string tenantId,
        JsonElement currentSettings,
        JsonElement newSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var currentEffective = await MergeTenantSettingsAsync(currentSettings, cancellationToken);
        var newEffective = await MergeTenantSettingsAsync(newSettings, cancellationToken);

        var findings = await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
            storeManager,
            tenantId,
            currentEffective,
            newEffective,
            cancellationToken
        );

        return findings
            .Select(finding => new SettingError(
                finding.Violation.CeilingName,
                $"The client '{finding.ClientId}' registers '{finding.Violation.SettingName}' value "
                    + $"'{finding.Violation.Value}', which the tenant's effective "
                    + $"'{finding.Violation.CeilingName}' would not permit."
            ))
            .ToList();
    }

    /// <summary>
    /// Updates a tenant's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The tenant's settings were updated.</response>
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

        var settingsValue = SerializeToElement(jsonObject);
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            settingsValue,
            OpenIdSettingScopes.Tenant,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var coherenceErrors = await ValidateClientCoherenceAsync(
            tenantId,
            tenantSettings.Value,
            settingsValue,
            storeManager,
            cancellationToken
        );
        if (coherenceErrors.Count > 0)
        {
            return SettingsValidationProblem(coherenceErrors);
        }

        tenantSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(tenantSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantChangedAsync(
            httpContext,
            tenantSettings.TenantId,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToTenantSettingsResource(tenantSettings)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantSettings.TenantId,
            settingsChanged: true,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = tenantSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a tenant's secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The tenant's secrets.</response>
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
    /// Creates a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="request">The <see cref="CreateSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created secret.</response>
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

        var validationError = SecretValidator.ValidateCreate(request);
        if (validationError is not null)
        {
            return ToErrorResult(validationError);
        }

        var secretId = CryptoService.GenerateResourceId();

        GeneratedSecret generatedSecret;
        try
        {
            generatedSecret = SecretGenerator.GenerateSecret(
                new GenerateSecretRequest
                {
                    SecretId = secretId,
                    SecretType = request.SecretType,
                    KeySizeBits = request.KeySizeBits,
                    Curve = request.Curve,
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
            await store.AddSecretAsync(tenantId, generatedSecret.Secret, cancellationToken);
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

        var secretResource = ToSecretResource(generatedSecret.Secret);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(secretResource),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        // The store assigns the secret's token on insert (ADR-0012), so no re-read is needed.
        httpContext.Response.Headers.ETag = generatedSecret.Secret.ConcurrencyToken;
        return TypedResults.Created($"/tenants/{tenantId}/secrets/{secretId}", secretResource);
    }

    /// <summary>
    /// Imports a caller-supplied public key (JWK, PEM, or certificate) as a new tenant secret.
    /// </summary>
    // See ADR-0104.
    internal virtual async ValueTask<IResult> ImportSecretAsync(
        HttpContext httpContext,
        [FromRoute] string tenantId,
        [FromBody] ImportSecretRequest request,
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

        var validationError = SecretValidator.ValidateImport(request);
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
            CreatedWhen = TimeProvider.GetUtcNow(),
            ExpiresWhen = request.ExpiresWhen,
        };
        if (!KeyImporter.TryImportKey(importRequest, out var importedSecret, out var importError))
        {
            return TypedResults.Problem(
                detail: importError,
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var keyIdConflict = KeyIdConflictOrDefault(tenant.Secrets.Value, importedSecret, "tenant");
        if (keyIdConflict is not null)
        {
            return keyIdConflict;
        }

        try
        {
            await store.AddSecretAsync(tenantId, importedSecret, cancellationToken);
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

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Created,
            ToResourceValues(importedSecretResource),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = importedSecret.ConcurrencyToken;
        return TypedResults.Created(
            $"/tenants/{tenantId}/secrets/{secretId}",
            importedSecretResource
        );
    }

    /// <summary>
    /// Gets a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested secret.</response>
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
    /// Gets the public key material of an asymmetric tenant secret, as a JWK and a PEM.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The public key material of the requested secret.</response>
    [EndpointName("api/tenants/secrets/get-public-key")]
    internal virtual async ValueTask<IResult> GetPublicKeyAsync(
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
            x => SecretPublicKeyConverter.Convert(x.Value)
        );
    }

    /// <summary>
    /// Updates a tenant secret's metadata.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> with the new metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was updated.</response>
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

        var metadataError = SecretValidator.ValidateUpdate(secret, request);
        if (metadataError is not null)
        {
            return ToErrorResult(metadataError);
        }

        var updated = new PersistedSecret
        {
            SecretId = secret.SecretId,
            KeyId = secret.KeyId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = request.Use,
            Algorithm = request.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
        };

        await store.UpdateSecretAsync(tenantId, updated, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToSecretResource(updated)),
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Deletes a tenant secret.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the OpenID Tenant.</param>
    /// <param name="secretId">The identifier of the secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The secret was deleted.</response>
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

        var secretValues = ToResourceValues(ToSecretResource(secret));

        await store.RemoveSecretAsync(tenantId, secretId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordTenantSecretChangedAsync(
            httpContext,
            tenantId,
            secretId,
            ResourceChangeTypes.Deleted,
            secretValues,
            cancellationToken
        );

        await NotifyTenantChangedAsync(
            httpContext,
            tenantId,
            settingsChanged: false,
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lists a tenant's owners.
    /// </summary>
    /// <response code="200">A page of the tenant's owners.</response>
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
    /// Adds an owner to a tenant.
    /// </summary>
    /// <response code="201">The owner was added.</response>
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
    /// Removes an owner from a tenant.
    /// </summary>
    /// <response code="204">The owner was removed.</response>
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

    // Announces a committed change to the tenant: this replica evicts it from the tenant cache, so the change takes effect
    // at once, and seeds it after a settings change, so a key its new settings require is provisioned (ADR-0169). The
    // mediator is request-scoped, so it is resolved from the request's services.
    private static ValueTask NotifyTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        bool settingsChanged,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new TenantChangedCommand(tenantId, settingsChanged),
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

    private async ValueTask<PersistedTenant> ReloadTenantAsync(
        PersistedTenant tenant,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();
        return await store.GetOrDefaultAsync(tenant.TenantId, cancellationToken) ?? tenant;
    }

    // An empty issuer means the tenant has none; storing it as empty would put it under the issuer's unique index.
    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
