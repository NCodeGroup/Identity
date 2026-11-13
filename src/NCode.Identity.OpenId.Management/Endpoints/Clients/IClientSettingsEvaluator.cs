// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Defines a service that resolves, validates and names the settings of an OpenID Client the way the authorization
/// server resolves them at runtime. The client endpoint handler and <see cref="IClientCreator"/> share it.
/// </summary>
internal interface IClientSettingsEvaluator
{
    /// <summary>
    /// Resolves the addressed tenant's effective settings: the team's effective settings merged with the tenant's
    /// persisted overrides. The tenant's effective view is reconstructed by id, so it is independent of the request's
    /// resolved tenant. Both the team's and the tenant's settings are resolved through the
    /// <see cref="IEffectiveSettingsResolver"/>, so each is expanded with the ceilings of the security profiles it
    /// selects, as it is at runtime.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The tenant's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the tenant's
    /// settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The tenant's document or its team's cannot be honored.</exception>
    ValueTask<IReadOnlySettingCollection?> MergeTenantSettingsAsync(
        string tenantId,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Merges an OpenID Client's persisted settings onto the addressed tenant's effective settings, producing the same
    /// effective <see cref="IReadOnlySettingCollection"/> the authorization server resolves for the client at runtime.
    /// The client's settings are resolved through the <see cref="IEffectiveSettingsResolver"/>, so they are expanded
    /// with the ceilings of the security profiles they select, as they are at runtime. A document that cannot be
    /// honored fails naming its owner.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client that owns the settings document.</param>
    /// <param name="clientSettings">The client's settings document to merge.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client's effective <see cref="IReadOnlySettingCollection"/>, or <c>null</c> when the owning
    /// tenant's settings are absent.</returns>
    /// <exception cref="UnreadableSettingsException">The client's document, the tenant's or its team's cannot be
    /// honored.</exception>
    ValueTask<IReadOnlySettingCollection?> MergeClientSettingsAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Verifies that a client's settings document, resolved onto its tenant's effective settings as the runtime
    /// resolves it, satisfies every registered <see cref="IClientCoherenceRule"/>.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientSettings">The client's settings document to verify.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances naming each registered setting whose value is not permitted,
    /// or an empty list.</returns>
    ValueTask<IReadOnlyList<SettingError>> ValidateEffectiveSettingsAsync(
        string tenantId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the client-authentication method a client with the specified settings document is registered for,
    /// through the <see cref="IClientSecretIssuer"/>, which stores the client's shared secrets in the form that method
    /// needs.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <param name="clientSettings">The client's settings document.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to load the tenant's persisted settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The client's effective <c>token_endpoint_auth_method</c>, or <see langword="null"/> when the settings
    /// cannot be merged.</returns>
    ValueTask<string?> GetTokenEndpointAuthMethodAsync(
        string tenantId,
        string clientId,
        JsonElement clientSettings,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Writes the client-authentication method a client is registered for into its settings document when the document
    /// names none, so every stored client names its method and a change to its platform's, team's or tenant's
    /// <c>token_endpoint_auth_method</c> never moves the method its shared secrets are stored for.
    /// </summary>
    /// <param name="clientSettings">The client's settings document.</param>
    /// <param name="tokenEndpointAuthMethod">The client's effective <c>token_endpoint_auth_method</c>, or
    /// <see langword="null"/> when it cannot be resolved.</param>
    /// <returns>The settings document that names the client's method.</returns>
    JsonElement NameTokenEndpointAuthMethod(
        JsonElement clientSettings,
        string? tokenEndpointAuthMethod
    );
}
