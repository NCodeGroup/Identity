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
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Platform;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

/*

GET   api/platform/{platformId}

GET   api/platform/{platformId}/settings
PATCH api/platform/{platformId}/settings
GET   api/platform/{platformId}/effective-settings

*/

/// <summary>
/// Provides the API endpoints for managing an OpenID Server, including its settings.
/// </summary>
internal class PlatformApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    IPlatformValidator serverValidator,
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<PlatformApiEndpointHandler> logger
) : BaseApiEndpointHandler, IEndpointProvider
{
    // The number of tenants read per page when a settings write is checked against every tenant's clients.
    private const int TenantPageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPlatformValidator PlatformValidator { get; } = serverValidator;
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
    private ILogger<PlatformApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var servers = endpoints
            .MapGroup("/platform")
            .WithTags(OpenIdConstants.EndpointTags.Platform);

        servers
            .MapPost("", CreatePlatformAsync)
            .Produces<PlatformResource>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        servers.MapGet("", ListPlatformsAsync).Produces<CollectionResource<PlatformResource>>();
        servers.MapGet("/{platformId}", GetPlatformAsync).Produces<PlatformResource>();
        servers
            .MapPatch("/{platformId}", UpdatePlatformAsync)
            .Produces(StatusCodes.Status204NoContent);

        servers
            .MapGet("/{platformId}/settings", GetSettingsAsync)
            .Produces<PlatformSettingsResource>();
        servers
            .MapPatch("/{platformId}/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        servers
            .MapGet("/{platformId}/effective-settings", GetEffectiveSettingsAsync)
            .Produces<PlatformEffectiveSettingsResource>();
    }

    /// <summary>
    /// Maps a <see cref="PersistedPlatform"/> to its <see cref="PlatformResource"/> representation.
    /// </summary>
    /// <param name="server">The <see cref="PersistedPlatform"/> to map.</param>
    /// <returns>The mapped <see cref="PlatformResource"/>.</returns>
    internal virtual PlatformResource ToPlatformResource(PersistedPlatform server)
    {
        return new PlatformResource
        {
            PlatformId = server.PlatformId,
            ConcurrencyToken = server.ConcurrencyToken,
            DisplayName = server.DisplayName,
        };
    }

    /// <summary>
    /// Maps a <see cref="PersistedPlatformSettings"/> to its <see cref="PlatformSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The <see cref="PersistedPlatformSettings"/> to map.</param>
    /// <returns>The mapped <see cref="PlatformSettingsResource"/>.</returns>
    internal virtual PlatformSettingsResource ToPlatformSettingsResource(
        PersistedPlatformSettings settings
    )
    {
        return new PlatformSettingsResource
        {
            PlatformId = settings.PlatformId,
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Lists the OpenID servers.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cursor">The opaque continuation token from a previous page, or <c>null</c> for the first page.</param>
    /// <param name="limit">The maximum number of servers to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">A page of servers.</response>
    [EndpointName("api/platform/list")]
    internal virtual async ValueTask<IResult> ListPlatformsAsync(
        HttpContext httpContext,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = NormalizeLimit(limit);

        // Server administration is central-admin; a null authorization resource restricts this to global admins.
        return await ProcessListAsync(
            httpContext,
            authorizationResource: null,
            async () =>
            {
                await using var storeManager = await StoreManagerFactory.CreateAsync(
                    cancellationToken
                );
                var store = storeManager.GetStore<IPlatformStore>();
                return await store.GetPageAsync(
                    new PageRequest { Cursor = cursor, Limit = effectiveLimit },
                    cancellationToken
                );
            },
            ToPlatformResource
        );
    }

    /// <summary>
    /// Gets an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The requested server.</response>
    [EndpointName("api/platform/get")]
    internal virtual async ValueTask<IResult> GetPlatformAsync(
        HttpContext httpContext,
        [FromRoute] string platformId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var server = await store.GetOrDefaultAsync(platformId, cancellationToken);

        return await ProcessGetAsync(httpContext, server, Operations.Read, ToPlatformResource);
    }

    /// <summary>
    /// Creates an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The <see cref="CreatePlatformRequest"/> describing the server to create.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The created server.</response>
    [EndpointName("api/platform/create")]
    internal virtual async ValueTask<IResult> CreatePlatformAsync(
        HttpContext httpContext,
        [FromBody] CreatePlatformRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var platformId = CryptoService.GenerateResourceId();

        var server = new PersistedPlatform
        {
            PlatformId = platformId,
            ConcurrencyToken = string.Empty,
            DisplayName = request.DisplayName,
            Settings = new PersistedPlatformSettings
            {
                PlatformId = platformId,
                ConcurrencyToken = string.Empty,
                Value = SerializeToElement(ToJsonObject(request.Settings)),
            },
        };

        // Core preconditions (authorization + uniqueness) are asserted by the validator (ADR-1014).
        var error = await PlatformValidator.ValidateCreateAsync(
            httpContext.User,
            server,
            storeManager,
            cancellationToken
        );

        if (error is not null)
        {
            return ToErrorResult(error);
        }

        // Setting names are a closed vocabulary (ADR-1139), and the server carries only the settings that apply within it
        // (ADR-1149): a settings document that cannot be honored is rejected here rather than stored and failed when the
        // server's settings are next loaded.
        var jsonOptions = httpContext.GetOpenIdContext().Environment.JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Platform,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        await store.AddAsync(server, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        var resource = ToPlatformResource(server);

        await ManagementAuditRecorder.RecordPlatformChangedAsync(
            httpContext,
            server.PlatformId,
            ResourceChangeTypes.Created,
            ToResourceValues(resource),
            cancellationToken
        );

        // The store assigns the row token on insert (ADR-1012), so no re-read is needed.
        httpContext.Response.Headers.ETag = server.ConcurrencyToken;
        return TypedResults.Created($"/platform/{server.PlatformId}", resource);
    }

    /// <summary>
    /// Updates an OpenID server.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="request">The JSON Patch document to apply to the server metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The server was updated.</response>
    [EndpointName("api/platform/update")]
    internal virtual async ValueTask<IResult> UpdatePlatformAsync(
        HttpContext httpContext,
        [FromRoute] string platformId,
        [FromBody] JsonPatchDocument<UpdatePlatformRequest> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var server = await store.GetOrDefaultAsync(platformId, cancellationToken);
        if (server is null)
        {
            return TypedResults.NotFound();
        }

        var model = new UpdatePlatformRequest { DisplayName = server.DisplayName };

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

        // Core preconditions (authorization + If-Match + required fields) are asserted by the validator
        // against the hydrated model (ADR-1014).
        var error = await PlatformValidator.ValidateUpdateAsync(
            httpContext.User,
            server,
            model,
            ifMatch,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ToErrorResult(error);
        }

        server.DisplayName = model.DisplayName;

        await store.UpdateAsync(server, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordPlatformChangedAsync(
            httpContext,
            server.PlatformId,
            ResourceChangeTypes.Updated,
            ToResourceValues(ToPlatformResource(server)),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Gets a server's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The server's settings.</response>
    [EndpointName("api/platform/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string platformId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(platformId, cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            serverSettings,
            Operations.Read,
            ToPlatformSettingsResource
        );
    }

    /// <summary>
    /// Gets a server's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The server's effective settings.</response>
    [EndpointName("api/platform/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string platformId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(platformId, cancellationToken);
        if (serverSettings is null)
        {
            return TypedResults.NotFound();
        }

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // The authorization is against the persisted settings, like the raw settings read. See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            serverSettings.Value,
            authorizationResource: serverSettings,
            Operations.Read,
            async (settings, token) =>
            {
                var effectiveSettings = await ResolveStoredPlatformSettingsAsync(
                    platformId,
                    settings,
                    token
                );
                return ToPlatformEffectiveSettingsResource(platformId, effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Resolves the effective settings of the server's committed settings document and names the server when the
    /// document cannot be honored by the settings vocabulary.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenID Server that owns the settings document.</param>
    /// <param name="settings">The server's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The server's effective settings.</returns>
    /// <exception cref="UnreadableSettingsException">The document cannot be honored.</exception>
    // See ADR-1245.
    private async ValueTask<IReadOnlySettingCollection> ResolveStoredPlatformSettingsAsync(
        string platformId,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await EffectiveSettingsResolver.ResolvePlatformAsync(
                settings,
                cancellationToken
            );
        }
        catch (JsonException exception)
        {
            throw new UnreadableSettingsException(
                OpenIdSettingScopes.Platform,
                platformId,
                exception
            );
        }
    }

    /// <summary>
    /// Maps an OpenID Server's effective <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="PlatformEffectiveSettingsResource"/> representation.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenID Server that owns the settings.</param>
    /// <param name="settings">The server's effective <see cref="IReadOnlySettingCollection"/>.</param>
    /// <returns>The mapped <see cref="PlatformEffectiveSettingsResource"/>.</returns>
    internal virtual PlatformEffectiveSettingsResource ToPlatformEffectiveSettingsResource(
        string platformId,
        IReadOnlySettingCollection settings
    )
    {
        return new PlatformEffectiveSettingsResource
        {
            PlatformId = platformId,
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Platform
            ),
        };
    }

    /// <summary>
    /// Updates a server's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The server's settings were updated.</response>
    [EndpointName("api/platform/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromRoute] string platformId,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var serverSettings = await store.GetSettingsOrDefaultAsync(platformId, cancellationToken);

        if (serverSettings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            serverSettings,
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        // Enforce the optimistic-concurrency precondition supplied by the caller. Without this check the
        // row we just loaded would always pass the store's concurrency comparison, silently overwriting
        // any changes made between the caller's read and this write (lost update).
        if (
            !string.IsNullOrEmpty(ifMatch)
            && !string.Equals(ifMatch, serverSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(serverSettings.Value);

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
            OpenIdSettingScopes.Platform,
            jsonOptions
        );
        if (settingErrors.Count > 0)
        {
            return SettingsValidationProblem(settingErrors);
        }

        var currentServerSettings = await ResolveReplacedPlatformSettingsOrDefaultAsync(
            platformId,
            serverSettings.Value,
            cancellationToken
        );
        var newServerSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
            settingsValue,
            cancellationToken
        );

        if (
            currentServerSettings is not null
            && ClientCoherenceValidator.ReadsChangedSetting(
                currentServerSettings,
                newServerSettings
            )
        )
        {
            var coherenceErrors = await ValidateClientCoherenceAsync(
                currentServerSettings,
                newServerSettings,
                storeManager,
                cancellationToken
            );
            if (coherenceErrors.Count > 0)
            {
                return SettingsValidationProblem(coherenceErrors);
            }
        }

        var previousSettings = serverSettings.Value;
        serverSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(serverSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await NotifyPlatformSettingsChangedAsync(
            httpContext,
            platformId,
            previousSettings,
            settingsValue,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = serverSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves the effective settings of the server's committed settings document, which a settings write replaces.
    /// When the document cannot be honored by the settings vocabulary, nothing was coherent under it, so no client is
    /// checked and the write may replace it with a document that can be honored.
    /// </summary>
    /// <param name="platformId">The identifier of the OpenID Server.</param>
    /// <param name="replacedSettings">The server's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The server's effective settings, or <see langword="null"/> when the document cannot be honored.</returns>
    // See ADR-1241.
    private async ValueTask<IReadOnlySettingCollection?> ResolveReplacedPlatformSettingsOrDefaultAsync(
        string platformId,
        JsonElement replacedSettings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await EffectiveSettingsResolver.ResolvePlatformAsync(
                replacedSettings,
                cancellationToken
            );
        }
        catch (JsonException exception)
        {
            Logger.PlatformSettingsUnreadable(platformId, exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Verifies that replacing the server's settings document leaves every value each client of every tenant registers
    /// permitted by every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The current and new
    /// server settings are each resolved through the <see cref="IEffectiveSettingsResolver"/>, and each tenant's
    /// document is resolved onto both. The tenants are read as an untracked, secret-free projection
    /// (<see cref="ITenantStore.GetSettingsPageAsync"/>), and the walk runs only when the effective value of a setting
    /// a coherence rule reads changed (<see cref="IClientCoherenceValidator.ReadsChangedSetting"/>). A tenant whose
    /// settings document cannot be honored is logged and skipped, because none of its clients is coherent under either
    /// settings.
    /// </summary>
    /// <param name="currentServerSettings">The server's effective settings with its current settings document.</param>
    /// <param name="newServerSettings">The server's effective settings with its new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenants and their clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the server setting that does not permit each value, or
    /// an empty list.</returns>
    // See ADR-1206 and ADR-1241.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        IReadOnlySettingCollection currentServerSettings,
        IReadOnlySettingCollection newServerSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        List<SettingError> errors = [];
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                IReadOnlySettingCollection currentTenantSettings;
                IReadOnlySettingCollection newTenantSettings;
                try
                {
                    currentTenantSettings = EffectiveSettingsResolver.Resolve(
                        currentServerSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    newTenantSettings = EffectiveSettingsResolver.Resolve(
                        newServerSettings,
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

    // Announces a committed change to the server's settings document, so every handler of the change (the default one
    // reads the document again on every replica, whose reconciliation then seeds the tenants and checks the clients,
    // ADR-1219) runs after the write. The mediator is request-scoped, so it is resolved from the request's services.
    private static ValueTask NotifyPlatformSettingsChangedAsync(
        HttpContext httpContext,
        string platformId,
        JsonElement previousSettings,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new PlatformSettingsChangedCommand(
                platformId,
                previousSettings.Clone(),
                settings.Clone()
            ),
            cancellationToken
        );
    }
}
