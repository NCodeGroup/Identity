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
using IdGen;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Authentication.Servers;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Authentication.Tenants.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Playground.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> <see cref="DefaultStaticSingleOpenIdTenantProvider"/> that provides
/// the tenant with an ephemeral, in-memory <c>RSA</c> signing key instead of loading persisted secrets.
/// This makes the server fully runnable (token signing and the JWKS endpoint) without any configured or
/// persisted secret keys.
/// </summary>
/// <remarks>
/// The key is generated in-process and is NOT persisted, so it is regenerated whenever the tenant is
/// (re)materialized. This is intentional for local development and testing and must never be used in
/// production, where a stable, securely-managed signing key is required.
/// </remarks>
public sealed class EphemeralStaticSingleOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IOpenIdServerProvider openIdServerProvider,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    IIdGenerator<long> idGenerator,
    ISecretKeyFactory secretKeyFactory
) : DefaultStaticSingleOpenIdTenantProvider(
    templateBinderFactory,
    optionsAccessor,
    openIdServerProvider,
    storeManagerFactory,
    tenantCache,
    settingSerializer,
    secretSerializer,
    secretKeyCollectionProviderFactory,
    collectionDataSourceFactory,
    settingCollectionProviderFactory,
    idGenerator)
{
    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    /// <inheritdoc />
    protected override ValueTask<AsyncSharedReferenceLease<ISecretKeyCollectionProvider>> GetTenantSecretsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        TenantDescriptor tenantDescriptor,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var signingKey = CreateEphemeralSigningKey();
        var provider = SecretKeyCollectionProviderFactory.CreateStatic([signingKey]);
        return ValueTask.FromResult(provider.AsSharedReference());
    }

    private SecretKey CreateEphemeralSigningKey()
    {
        using var rsa = RSA.Create(2048);

        var metadata = new KeyMetadata
        {
            KeyId = Guid.NewGuid().ToString("N"),
            Use = SecretKeyUses.Signature,
            Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256
        };

        return SecretKeyFactory.CreateRsa(metadata, rsa);
    }
}
