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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Logic;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Playground.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> <see cref="DefaultOpenIdTenantFactory"/> that ensures the tenant has a
/// persisted <c>RSA</c> signing key, generating and storing one through the normal persistence path when absent,
/// instead of injecting an in-memory key. Unlike <see cref="EphemeralOpenIdTenantFactory"/>, the key is a single
/// source of truth: the runtime, the JWKS endpoint, and the management API all read it from the store, and it
/// survives restarts when paired with a persistent database and a persistent data-protection key ring.
/// </summary>
/// <remarks>
/// The seed is self-healing: if the persisted signing key cannot be unprotected (for example, the data-protection
/// key ring was deleted), a new key is generated and stored so the server stays runnable. This is intended for
/// local development only and must never be used in production, where a stable, securely-managed signing key with a
/// deliberate rotation policy is required.
/// </remarks>
public sealed class DeveloperSigningKeyOpenIdTenantFactory(
    ITenantResolver tenantResolver,
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    ISecretGenerator secretGenerator,
    ICryptoService cryptoService,
    TimeProvider timeProvider
)
    : DefaultOpenIdTenantFactory(
        tenantResolver,
        templateBinderFactory,
        optionsAccessor,
        storeManagerFactory,
        tenantCache,
        settingCollectionProviderFactory,
        settingSerializer,
        secretSerializer,
        secretKeyCollectionProviderFactory,
        collectionDataSourceFactory
    )
{
    private const int RsaKeySizeBits = 2048;

    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    protected override async ValueTask<
        AsyncSharedReferenceLease<ISecretKeyCollectionProvider>
    > GetTenantSecretsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        PersistedTenant persistedTenant,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        await EnsureSigningKeyAsync(persistedTenant, cancellationToken);

        // Delegate to the production read path so the runtime, JWKS, and the management API share one source.
        return await base.GetTenantSecretsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            persistedTenant,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            propertyBag,
            cancellationToken
        );
    }

    private async ValueTask EnsureSigningKeyAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        if (HasUsableSecret(persistedTenant.Secrets))
        {
            return;
        }

        var generatedSecret = SecretGenerator.GenerateSecret(
            new GenerateSecretRequest
            {
                SecretId = CryptoService.GenerateResourceId(),
                SecretType = SecretTypes.Rsa,
                KeySizeBits = RsaKeySizeBits,
                Use = SecretKeyUses.Signature,
                Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
                CreatedWhen = TimeProvider.GetUtcNow(),
                ExpiresWhen = TimeProvider.GetUtcNow().AddYears(100),
            }
        );

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        await store.AddSecretAsync(persistedTenant.TenantId, generatedSecret, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        // Refresh the loaded state so the base read path observes the newly-seeded secret.
        persistedTenant.Secrets = await store.GetSecretsAsync(
            persistedTenant.TenantId,
            cancellationToken
        );
    }

    private bool HasUsableSecret(PersistedTenantSecrets secrets)
    {
        if (secrets.Value.Count == 0)
        {
            return false;
        }

        // A secret protected by a now-unavailable data-protection key cannot be unprotected; treat that as
        // "no usable key" so the dev key self-heals (regenerates) rather than failing every request.
        IReadOnlyCollection<SecretKey> secretKeys;
        try
        {
            secretKeys = SecretSerializer.DeserializeSecrets(secrets.Value);
        }
        catch (Exception exception)
            when (exception
                    is CryptographicException
                        or FormatException
                        or InvalidOperationException
            )
        {
            return false;
        }

        try
        {
            return secretKeys.Count > 0;
        }
        finally
        {
            foreach (var secretKey in secretKeys)
            {
                if (secretKey is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
