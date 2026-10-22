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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Models;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.Server;

/// <summary>
/// A <strong>development-only</strong> <see cref="DefaultOpenIdTenantFactory"/> that provides
/// the tenant with an ephemeral, in-memory <c>RSA</c> signing key instead of loading persisted secrets.
/// This makes the server fully runnable (token signing and the JWKS endpoint) without any configured or
/// persisted secret keys.
/// </summary>
/// <remarks>
/// The key is generated in-process and is NOT persisted: each tenant keeps one key for the life of the process,
/// so evicting the tenant from the cache (a management write does) does not invalidate the tokens it issued, and
/// a restart regenerates it. This is intentional for local development and testing and must never be used in
/// production, where a stable, securely-managed signing key is required.
/// </remarks>
public sealed class EphemeralOpenIdTenantFactory(
    ITenantResolver tenantResolver,
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    ISecretKeyFactory secretKeyFactory
)
    : DefaultOpenIdTenantFactory(
        tenantResolver,
        optionsAccessor,
        storeManagerFactory,
        tenantCache,
        settingCollectionProviderFactory,
        settingSerializer,
        secretSerializer,
        secretKeyCollectionProviderFactory,
        collectionDataSourceFactory,
        effectiveSettingsResolver
    )
{
    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    // One key per tenant for the life of the process: a tenant is materialized again after every management write
    // evicts it (ADR-0169), and a new key would invalidate every token the tenant issued.
    private ConcurrentDictionary<string, RsaSecretKey> SigningKeys { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    protected override ValueTask<
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
        var signingKey = SigningKeys.GetOrAdd(
            persistedTenant.TenantId,
            _ => CreateEphemeralSigningKey()
        );
        var provider = SecretKeyCollectionProviderFactory.CreateStatic([signingKey]);
        return ValueTask.FromResult(provider.AsSharedReference());
    }

    private RsaSecretKey CreateEphemeralSigningKey()
    {
        using var rsa = RSA.Create(2048);

        var metadata = new KeyMetadata
        {
            KeyId = Guid.NewGuid().ToString("N"),
            Use = SecretKeyUses.Signature,
            Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
        };

        return SecretKeyFactory.CreateRsa(metadata, rsa);
    }
}
