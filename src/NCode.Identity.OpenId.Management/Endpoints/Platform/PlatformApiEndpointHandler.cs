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
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.OpenId.Management.Contracts.Platform;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.EncryptionKeys;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Exceptions;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

/*

GET   api/platform/settings
PATCH api/platform/settings
GET   api/platform/effective-settings

GET   api/platform/encryption-keys
POST  api/platform/encryption-keys/rekey

*/

/// <summary>
/// Provides the API endpoints for the platform's settings and master keys. The platform is the one deployment root, so its routes carry
/// no identifier.
/// </summary>
internal class PlatformApiEndpointHandler(
    IStoreManagerFactory storeManagerFactory,
    IAuthorizationService authorizationService,
    ICryptoService cryptoService,
    ISettingSerializer settingSerializer,
    ISettingScopeCollectionProvider settingScopeCollectionProvider,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    ITenantClientCoherenceChecker tenantClientCoherenceChecker,
    IMasterKeyManager masterKeyManager,
    IManagementAuditRecorder managementAuditRecorder,
    ILogger<PlatformApiEndpointHandler> logger
) : BaseApiEndpointHandler, IEndpointProvider
{
    // The number of tenants read per page when a settings write is checked against every tenant's clients.
    private const int TenantPageSize = 100;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;

    private ISettingScopeCollectionProvider SettingScopeCollectionProvider { get; } =
        settingScopeCollectionProvider;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private ITenantClientCoherenceChecker TenantClientCoherenceChecker { get; } =
        tenantClientCoherenceChecker;
    private IMasterKeyManager MasterKeyManager { get; } = masterKeyManager;
    private IManagementAuditRecorder ManagementAuditRecorder { get; } = managementAuditRecorder;
    private ILogger<PlatformApiEndpointHandler> Logger { get; } = logger;

    /// <inheritdoc />
    protected override IAuthorizationService AuthorizationService { get; } = authorizationService;

    /// <inheritdoc />
    protected override ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var platform = endpoints
            .MapGroup("/platform")
            .WithTags(OpenIdConstants.EndpointTags.Platform);

        platform.MapGet("/settings", GetSettingsAsync).Produces<PlatformSettingsResource>();
        platform
            .MapPatch("/settings", UpdateSettingsAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        platform
            .MapGet("/effective-settings", GetEffectiveSettingsAsync)
            .Produces<PlatformEffectiveSettingsResource>();
        platform
            .MapGet("/encryption-keys", GetEncryptionKeysAsync)
            .Produces<EncryptionKeysResource>();
        platform
            .MapPost("/encryption-keys/rekey", RekeyEncryptionKeysAsync)
            .Produces<EncryptionKeyRotationResource>()
            .ProducesProblem(StatusCodes.Status409Conflict);
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
            ConcurrencyToken = settings.ConcurrencyToken,
            Settings = settings.Value,
        };
    }

    /// <summary>
    /// Gets the platform's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The platform's settings.</response>
    [EndpointName("api/platform/settings/get")]
    internal virtual async ValueTask<IResult> GetSettingsAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var platformSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);

        return await ProcessGetAsync(
            httpContext,
            platformSettings,
            authorizationResource: GetPlatformNode(httpContext),
            Operations.Read,
            ToPlatformSettingsResource
        );
    }

    // The platform's authority node: the one deployment root, addressed from the tenant the request runs under.
    private static ResourceNode GetPlatformNode(HttpContext httpContext) =>
        ResourceNode.For(
            httpContext.GetOpenIdContext().Tenant.TenantId,
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId
        );

    /// <summary>
    /// Gets the platform's effective (resolved) settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The platform's effective settings.</response>
    [EndpointName("api/platform/effective-settings/get")]
    internal virtual async ValueTask<IResult> GetEffectiveSettingsAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var platformSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);
        if (platformSettings is null)
        {
            return TypedResults.NotFound();
        }

        // The settings are resolved only after authorization, so a caller who may not read them learns nothing about a
        // document that cannot be honored, and the stored document's token is not the effective view's.
        // The authorization is against the persisted settings, like the raw settings read. See ADR-1245.
        return await ProcessGetAsync(
            httpContext,
            platformSettings.Value,
            authorizationResource: GetPlatformNode(httpContext),
            Operations.Read,
            async (settings, token) =>
            {
                var effectiveSettings = await ResolveStoredPlatformSettingsAsync(settings, token);
                return ToPlatformEffectiveSettingsResource(effectiveSettings);
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Gets the status of the platform's master keys. The response never carries key material.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The platform's encryption keys.</response>
    /// <response code="404">The platform does not exist.</response>
    [EndpointName("api/platform/encryption-keys/get")]
    internal virtual async ValueTask<IResult> GetEncryptionKeysAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await PlatformExistsAsync(cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetPlatformNode(httpContext),
            Operations.Read
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        return await EncryptionKeyEndpoints.GetAsync(
            MasterKeyManager,
            SecretOwner.Platform,
            cancellationToken
        );
    }

    /// <summary>
    /// Rekeys the platform: retires its active master key and creates the next version, which protects the Data
    /// Protection keys and host data created afterwards.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The new active key and the key that it retired.</response>
    /// <response code="404">The platform does not exist.</response>
    /// <response code="409">The last rotation is younger than twice the master key refresh interval (the response carries Retry-After).</response>
    /// <response code="412">Another rekey of the same master key committed first.</response>
    [EndpointName("api/platform/encryption-keys/rekey")]
    internal virtual async ValueTask<IResult> RekeyEncryptionKeysAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await PlatformExistsAsync(cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetPlatformNode(httpContext),
            Operations.Update
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        return await EncryptionKeyEndpoints.RekeyAsync(
            MasterKeyManager,
            ManagementAuditRecorder,
            httpContext,
            SecretOwner.Platform,
            cancellationToken
        );
    }

    private async ValueTask<bool> PlatformExistsAsync(CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var platform = await storeManager
            .GetStore<IPlatformStore>()
            .GetOrDefaultAsync(cancellationToken);
        return platform is not null;
    }

    /// <summary>
    /// Resolves the effective settings of the platform's committed settings document and names the platform when the
    /// document cannot be honored by the settings vocabulary.
    /// </summary>
    /// <param name="settings">The platform's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The platform's effective settings.</returns>
    /// <exception cref="UnreadableSettingsException">The document cannot be honored.</exception>
    // See ADR-1245.
    private async ValueTask<IReadOnlySettingCollection> ResolveStoredPlatformSettingsAsync(
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
                OpenIdConstants.PlatformResourceId,
                exception
            );
        }
    }

    /// <summary>
    /// Maps the platform's effective <see cref="IReadOnlySettingCollection"/> to its
    /// <see cref="PlatformEffectiveSettingsResource"/> representation.
    /// </summary>
    /// <param name="settings">The platform's effective <see cref="IReadOnlySettingCollection"/>.</param>
    /// <returns>The mapped <see cref="PlatformEffectiveSettingsResource"/>.</returns>
    internal virtual PlatformEffectiveSettingsResource ToPlatformEffectiveSettingsResource(
        IReadOnlySettingCollection settings
    )
    {
        return new PlatformEffectiveSettingsResource
        {
            Settings = ToEffectiveSettingsElement(
                settings,
                SettingScopeCollectionProvider.Collection,
                OpenIdSettingScopes.Platform
            ),
        };
    }

    /// <summary>
    /// Updates the platform's settings.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="request">The JSON Patch document to apply to the settings.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> concurrency token that must match the current settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="204">The platform's settings were updated.</response>
    [EndpointName("api/platform/settings/update")]
    internal virtual async ValueTask<IResult> UpdateSettingsAsync(
        HttpContext httpContext,
        [FromBody] JsonPatchDocument<JsonObject> request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IPlatformStore>();

        var platformSettings = await store.GetSettingsOrDefaultAsync(cancellationToken);

        if (platformSettings is null)
        {
            return TypedResults.NotFound();
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            GetPlatformNode(httpContext),
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
            && !string.Equals(ifMatch, platformSettings.ConcurrencyToken, StringComparison.Ordinal)
        )
        {
            return TypedResults.StatusCode(StatusCodes.Status412PreconditionFailed);
        }

        var jsonObject = ToJsonObject(platformSettings.Value);

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

        var currentPlatformSettings = await ResolveReplacedPlatformSettingsOrDefaultAsync(
            platformSettings.Value,
            cancellationToken
        );
        var newPlatformSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
            settingsValue,
            cancellationToken
        );

        if (
            currentPlatformSettings is not null
            && ClientCoherenceValidator.ReadsChangedSetting(
                currentPlatformSettings,
                newPlatformSettings
            )
        )
        {
            var coherenceErrors = await ValidateClientCoherenceAsync(
                currentPlatformSettings,
                newPlatformSettings,
                storeManager,
                cancellationToken
            );
            if (coherenceErrors.Count > 0)
            {
                return SettingsValidationProblem(coherenceErrors);
            }
        }

        var previousSettings = platformSettings.Value;
        platformSettings.Value = settingsValue;

        await store.UpdateSettingsAsync(platformSettings, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        await ManagementAuditRecorder.RecordPlatformChangedAsync(
            httpContext,
            ResourceChangeTypes.SettingsUpdated,
            ToResourceValues(ToPlatformSettingsResource(platformSettings)),
            cancellationToken
        );

        await NotifyPlatformSettingsChangedAsync(
            httpContext,
            previousSettings,
            settingsValue,
            cancellationToken
        );

        httpContext.Response.Headers.ETag = platformSettings.ConcurrencyToken;

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Resolves the effective settings of the platform's committed settings document, which a settings write replaces.
    /// When the document cannot be honored by the settings vocabulary, nothing was coherent under it, so no client is
    /// checked and the write may replace it with a document that can be honored.
    /// </summary>
    /// <param name="replacedSettings">The platform's committed settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The platform's effective settings, or <see langword="null"/> when the document cannot be honored.</returns>
    // See ADR-1241.
    private async ValueTask<IReadOnlySettingCollection?> ResolveReplacedPlatformSettingsOrDefaultAsync(
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
            Logger.PlatformSettingsUnreadable(
                OpenIdConstants.PlatformResourceId,
                exception.Message
            );
            return null;
        }
    }

    /// <summary>
    /// Verifies that replacing the platform's settings document leaves every value each client of every tenant
    /// registers permitted by every client coherence rule (<see cref="ITenantClientCoherenceChecker"/>). The walk
    /// follows the settings cascade (host configuration, platform, team, tenant): the current and new platform settings
    /// are each resolved through the <see cref="IEffectiveSettingsResolver"/>, each team's document is resolved onto
    /// both, and each of the team's tenants' documents is resolved onto those. The teams are paged (<see
    /// cref="ITeamStore.GetPageAsync(PageRequest, CancellationToken)"/>), and each team's tenants are read as an
    /// untracked, secret-free projection (<see cref="ITenantStore.GetSettingsPageAsync(string, PageRequest,
    /// CancellationToken)"/>). The walk runs only when the effective value of a setting a coherence rule reads changed
    /// (<see cref="IClientCoherenceValidator.ReadsChangedSetting"/>). A team or tenant whose settings document cannot
    /// be honored is logged and skipped, because none of its clients is coherent under either settings.
    /// </summary>
    /// <param name="currentPlatformSettings">The platform's effective settings with its current settings
    /// document.</param>
    /// <param name="newPlatformSettings">The platform's effective settings with its new settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the teams, tenants and clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the platform setting that does not permit each
    /// value, or an empty list.</returns>
    // See ADR-1206 and ADR-1241.
    internal virtual async ValueTask<IReadOnlyList<SettingError>> ValidateClientCoherenceAsync(
        IReadOnlySettingCollection currentPlatformSettings,
        IReadOnlySettingCollection newPlatformSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        List<SettingError> errors = [];
        var teamStore = storeManager.GetStore<ITeamStore>();
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var teamPage = new PageRequest { Limit = TenantPageSize };
        do
        {
            var teamResult = await teamStore.GetPageAsync(teamPage, cancellationToken);
            foreach (var team in teamResult.Items)
            {
                IReadOnlySettingCollection currentTeamSettings;
                IReadOnlySettingCollection newTeamSettings;
                try
                {
                    currentTeamSettings = EffectiveSettingsResolver.Resolve(
                        currentPlatformSettings,
                        OpenIdSettingScopes.Team,
                        team.Settings.Value
                    );
                    newTeamSettings = EffectiveSettingsResolver.Resolve(
                        newPlatformSettings,
                        OpenIdSettingScopes.Team,
                        team.Settings.Value
                    );
                }
                catch (JsonException exception)
                {
                    Logger.TeamSettingsUnreadable(team.TeamId, exception.Message);
                    continue;
                }

                var page = new PageRequest { Limit = TenantPageSize };
                do
                {
                    var result = await tenantStore.GetSettingsPageAsync(
                        team.TeamId,
                        page,
                        cancellationToken
                    );
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
                            Logger.TenantSettingsUnreadable(
                                tenantSettings.TenantId,
                                exception.Message
                            );
                            continue;
                        }

                        var findings =
                            await TenantClientCoherenceChecker.FindIntroducedViolationsAsync(
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
            }

            teamPage = teamPage with { Cursor = teamResult.NextCursor };
        } while (teamPage.Cursor is not null);

        return errors;
    }

    // Announces a committed change to the platform's settings document, so every handler of the change (the default one
    // reads the document again on every replica, whose reconciliation then seeds the tenants and checks the clients,
    // ADR-1219) runs after the write. The mediator is request-scoped, so it is resolved from the request's services.
    private static ValueTask NotifyPlatformSettingsChangedAsync(
        HttpContext httpContext,
        JsonElement previousSettings,
        JsonElement settings,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new PlatformSettingsChangedCommand(previousSettings.Clone(), settings.Clone()),
            cancellationToken
        );
    }
}
