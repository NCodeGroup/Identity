// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientSettingsEvaluator"/>.
/// </summary>
internal sealed class DefaultClientSettingsEvaluator(
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IClientCoherenceValidator clientCoherenceValidator,
    IClientSecretIssuer clientSecretIssuer
) : IClientSettingsEvaluator
{
    // See ADR-1466.
    internal const string MachineClientGrantTypesDetail =
        "A machine client obtains tokens only for itself: its 'grant_types_supported' is 'client_credentials' alone.";

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;
    private IClientSecretIssuer ClientSecretIssuer { get; } = clientSecretIssuer;

    /// <summary>
    /// Determines whether a client settings document limits the client to the <c>client_credentials</c> grant alone.
    /// </summary>
    /// <param name="settings">The client's settings document.</param>
    /// <returns>
    /// <see langword="true"/> when <c>grant_types_supported</c> is an array holding exactly the one string
    /// <c>client_credentials</c>; otherwise, <see langword="false"/>.
    /// </returns>
    // See ADR-1466.
    internal static bool IsClientCredentialsOnly(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (
            !settings.TryGetProperty(OpenIdSettingNames.GrantTypesSupported, out var grantTypes)
            || grantTypes.ValueKind != JsonValueKind.Array
            || grantTypes.GetArrayLength() != 1
        )
        {
            return false;
        }

        var grantType = grantTypes[0];
        return grantType.ValueKind == JsonValueKind.String
            && grantType.GetString() == OpenIdProtocol.GrantTypes.ClientCredentials;
    }

    /// <inheritdoc />
    // See ADR-1204 and ADR-1245.
    public async ValueTask<IReadOnlySettingCollection?> MergeTenantSettingsAsync(
        string tenantId,
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
        return EffectiveSettingsResolver.ResolveStored(
            teamEffective,
            OpenIdSettingScopes.Tenant,
            tenantId,
            tenant.Settings.Value
        );
    }

    /// <inheritdoc />
    // See ADR-1204 and ADR-1245.
    public async ValueTask<IReadOnlySettingCollection?> MergeClientSettingsAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenantEffective = await MergeTenantSettingsAsync(
            tenantId,
            storeManager,
            cancellationToken
        );
        return tenantEffective is null
            ? null
            : EffectiveSettingsResolver.ResolveStored(
                tenantEffective,
                OpenIdSettingScopes.Client,
                clientId,
                clientSettings
            );
    }

    /// <inheritdoc />
    // See ADR-1206.
    public async ValueTask<IReadOnlyList<SettingError>> ValidateEffectiveSettingsAsync(
        string tenantId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var tenantSettings = await MergeTenantSettingsAsync(
            tenantId,
            storeManager,
            cancellationToken
        );

        return tenantSettings is null
            ? []
            : ClientCoherenceValidator
                .Validate(tenantSettings, clientSettings)
                .Select(violation => new SettingError(violation.SettingName, violation.Description))
                .ToList();
    }

    /// <inheritdoc />
    // See ADR-1391.
    public async ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
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

        return await ClientSecretIssuer.GetTokenEndpointAuthMethodAsync(
            tenant.TeamId,
            tenantId,
            tenant.Settings.Value,
            clientId,
            clientSettings,
            cancellationToken
        );
    }

    /// <inheritdoc />
    // See ADR-1391.
    public JsonElement NameTokenEndpointAuthMethod(
        JsonElement clientSettings,
        string? tokenEndpointAuthMethod
    )
    {
        if (tokenEndpointAuthMethod is null)
        {
            return clientSettings;
        }

        var settings = ToJsonObject(clientSettings);
        if (settings.ContainsKey(OpenIdSettingNames.TokenEndpointAuthMethod))
        {
            return clientSettings;
        }

        settings[OpenIdSettingNames.TokenEndpointAuthMethod] = tokenEndpointAuthMethod;
        return JsonSerializer.SerializeToElement(settings);
    }

    private static JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? [];

            default:
                return [];
        }
    }
}
