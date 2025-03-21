﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using IdGen;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that uses a static single tenant configuration.
/// </summary>
[PublicAPI]
public class DefaultStaticSingleOpenIdTenantProvider(
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
    IIdGenerator<long> idGenerator
) : OpenIdTenantProvider, IAsyncDisposable
{
    private AsyncSharedReferenceLease<OpenIdTenant> CachedTenant { get; set; }

    private StaticSingleOpenIdTenantOptions TenantOptions =>
        OpenIdOptions.Tenant.StaticSingle ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.StaticSingle;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    protected override TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;

    /// <inheritdoc />
    protected override OpenIdOptions OpenIdOptions { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    protected override IOpenIdServerProvider OpenIdServerProvider { get; } = openIdServerProvider;

    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IOpenIdTenantCache TenantCache { get; } = tenantCache;

    /// <inheritdoc />
    protected override IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } = settingCollectionProviderFactory;

    /// <inheritdoc />
    protected override ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <inheritdoc />
    protected override ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <inheritdoc />
    protected override ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } = secretKeyCollectionProviderFactory;

    /// <inheritdoc />
    protected override ICollectionDataSourceFactory CollectionDataSourceFactory { get; } = collectionDataSourceFactory;

    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CachedTenant.DisposeAsync();
        CachedTenant = default;
    }

    /// <inheritdoc />
    public override async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> GetTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (CachedTenant.IsActive)
            return CachedTenant.AddReference();

        var tenantReference = await base.GetTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            propertyBag,
            cancellationToken
        );
        CachedTenant = tenantReference;

        return CachedTenant.AddReference();
    }

    /// <inheritdoc />
    protected override ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var options = TenantOptions;

        var tenantId = options.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;

        var displayName = options.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName;

        var descriptor = new TenantDescriptor
        {
            TenantId = tenantId,
            DisplayName = displayName
        };

        return ValueTask.FromResult(descriptor);
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedTenant?> TryGetTenantByIdAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (persistedTenant is not null)
            return persistedTenant;

        persistedTenant = CreateEmptyPersistedTenant(tenantId);

        await store.AddAsync(persistedTenant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return persistedTenant;
    }

    /// <summary>
    /// Creates an empty <see cref="PersistedTenant"/> instance with the specified tenant ID.
    /// </summary>
    protected internal virtual PersistedTenant CreateEmptyPersistedTenant(string tenantId)
    {
        var settingsJson = JsonSerializer.SerializeToElement(null, typeof(object));
        var settingsState = ConcurrentStateFactory.Create(settingsJson, Guid.NewGuid().ToString("N"));

        var secretsState = ConcurrentStateFactory.Create<IReadOnlyCollection<PersistedSecret>>(
            Array.Empty<PersistedSecret>(),
            Guid.NewGuid().ToString("N")
        );

        return new PersistedTenant
        {
            Id = IdGenerator.CreateId(),
            TenantId = tenantId,
            DomainName = null,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsDisabled = false,
            DisplayName = TenantOptions.DisplayName,
            SettingsState = settingsState,
            SecretsState = secretsState
        };
    }
}
