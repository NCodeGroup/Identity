// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientCreator"/>.
/// </summary>
internal sealed class DefaultClientCreator(
    IClientValidator clientValidator,
    IClientSettingsEvaluator clientSettingsEvaluator,
    ISettingSerializer settingSerializer,
    IClientKeyImporter clientKeyImporter,
    IClientKeySourceChecker clientKeySourceChecker,
    IClientJwksUriChecker clientJwksUriChecker,
    TimeProvider timeProvider,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider
) : IClientCreator
{
    private IClientValidator ClientValidator { get; } = clientValidator;
    private IClientSettingsEvaluator ClientSettingsEvaluator { get; } = clientSettingsEvaluator;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private IClientKeySourceChecker ClientKeySourceChecker { get; } = clientKeySourceChecker;
    private IClientJwksUriChecker ClientJwksUriChecker { get; } = clientJwksUriChecker;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;

    /// <inheritdoc />
    // See ADR-1014, ADR-1139, ADR-1149, ADR-1161, ADR-1206, ADR-1391 and ADR-1466.
    public async ValueTask<ClientCreation> StageAsync(
        IStoreManager storeManager,
        ClientCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ClientIdRules.IsValid(request.ClientId))
        {
            return ClientCreation.Failed(
                BadRequest(
                    $"The client_id must be 1 to {ClientIdRules.MaxLength} unreserved URI characters."
                )
            );
        }

        var client = new PersistedClient
        {
            TenantId = request.TenantId,
            ClientId = request.ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = request.IsDisabled,
            ResourceServerId = request.ResourceServerId,
            OwningNodeType = request.OwningNodeType,
            OwningNodeId = request.OwningNodeId,
            Settings = new PersistedClientSettings
            {
                TenantId = request.TenantId,
                ClientId = request.ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(
                    ClientSettingsJson.ToJsonObject(request.Settings)
                ),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = request.TenantId,
                ClientId = request.ClientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        // Core preconditions (uniqueness, the resource server) are asserted by the validator (ADR-1014).
        var error = await ClientValidator.ValidateCreateAsync(
            client,
            storeManager,
            cancellationToken
        );
        if (error is not null)
        {
            return ClientCreation.Failed(error);
        }

        // See ADR-1466.
        if (
            request.OwningNodeType is not null
            && !DefaultClientSettingsEvaluator.IsClientCredentialsOnly(client.Settings.Value)
        )
        {
            return ClientCreation.InvalidSettings(
                ClientSettingsJson.ToSettingErrors([
                    new SettingError(
                        OpenIdSettingNames.GrantTypesSupported,
                        DefaultClientSettingsEvaluator.MachineClientGrantTypesDetail
                    ),
                ])
            );
        }

        // Setting names are a closed vocabulary (ADR-1139), and a client carries only the settings that apply within it
        // (ADR-1149): a settings document that cannot be honored is rejected here rather than stored and failed on
        // every request that resolves the client.
        var settingErrors = SettingSerializer.ValidateSettings(
            request.Settings,
            OpenIdSettingScopes.Client,
            OpenIdEnvironmentProvider.Get().JsonSerializerOptions
        );
        if (settingErrors.Count > 0)
        {
            return ClientCreation.InvalidSettings(
                ClientSettingsJson.ToSettingErrors(settingErrors)
            );
        }

        var effectiveSettingErrors = await ClientSettingsEvaluator.ValidateEffectiveSettingsAsync(
            client.TenantId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );
        if (effectiveSettingErrors.Count > 0)
        {
            return ClientCreation.InvalidSettings(
                ClientSettingsJson.ToSettingErrors(effectiveSettingErrors)
            );
        }

        var jwksUriRefusal = await ClientJwksUriChecker.GetRefusalAsync(
            client.Settings.Value,
            null,
            cancellationToken
        );
        if (jwksUriRefusal is not null)
        {
            return ClientCreation.InvalidSettings(
                ClientSettingsJson.ToSettingErrors([jwksUriRefusal.Value])
            );
        }

        if (request.Jwks is { } jwks)
        {
            // A client's public keys come from one source: the keys registered for it or its jwks_uri (ADR-1161).
            if (ClientKeySourceChecker.HasJwksUri(client.Settings.Value))
            {
                return ClientCreation.Failed(
                    BadRequest("A client registers its keys by value or by jwks_uri, never both.")
                );
            }

            if (
                !ClientKeyImporter.TryImportRegisteredKeySet(
                    jwks,
                    TimeProvider.GetUtcNow(),
                    out var secrets,
                    out var importError
                )
            )
            {
                return ClientCreation.Failed(BadRequest(importError));
            }

            client.Secrets.Value = secrets;
        }

        var tokenEndpointAuthMethod = await ClientSettingsEvaluator.GetTokenEndpointAuthMethodAsync(
            client.TenantId,
            client.ClientId,
            client.Settings.Value,
            storeManager,
            cancellationToken
        );
        client.Settings.Value = ClientSettingsEvaluator.NameTokenEndpointAuthMethod(
            client.Settings.Value,
            tokenEndpointAuthMethod
        );

        await storeManager.GetStore<IClientStore>().AddAsync(client, cancellationToken);

        return ClientCreation.Staged(client);
    }

    private static ManagementError BadRequest(string detail) =>
        new() { StatusCode = StatusCodes.Status400BadRequest, Detail = detail };
}
