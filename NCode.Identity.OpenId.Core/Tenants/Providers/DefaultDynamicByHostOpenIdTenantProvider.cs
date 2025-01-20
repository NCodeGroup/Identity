#region Copyright Preamble

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

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that dynamically loads the tenant
/// configuration using the host name from an HTTP request.
/// </summary>
public class DefaultDynamicByHostOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    OpenIdEnvironment openIdEnvironment,
    IOpenIdErrorFactory openIdErrorFactory,
    IOpenIdServerProvider openIdServerProvider,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory
) : OpenIdTenantProvider
{
    private Regex? DomainNameRegex { get; set; }

    private DynamicByHostOpenIdTenantOptions TenantOptions =>
        OpenIdOptions.Tenant.DynamicByHost ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByHost;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    protected override TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;

    /// <inheritdoc />
    protected override OpenIdOptions OpenIdOptions { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    protected override OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    /// <inheritdoc />
    protected override IOpenIdErrorFactory OpenIdErrorFactory { get; } = openIdErrorFactory;

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

    private async ValueTask<PersistedTenant> GetTenantByDomainAsync(string domainName, CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.TryGetByDomainNameAsync(domainName, cancellationToken);
        if (persistedTenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with domain '{domainName}' could not be found.");

        return persistedTenant;
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var options = TenantOptions;

        var regex = DomainNameRegex ??= new Regex(
            options.RegexPattern,
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant |
            RegexOptions.Singleline);

        var host = httpContext.Request.Host.Host;
        var match = regex.Match(host);
        var domainName = match.Success ? match.Value : host;

        var persistedTenant = await GetTenantByDomainAsync(domainName, cancellationToken);
        propertyBag.Set(persistedTenant);

        return new TenantDescriptor
        {
            TenantId = persistedTenant.TenantId,
            DisplayName = persistedTenant.DisplayName,
            DomainName = persistedTenant.DomainName
        };
    }
}
