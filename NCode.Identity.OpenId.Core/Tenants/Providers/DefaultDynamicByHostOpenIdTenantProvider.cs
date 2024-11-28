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
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that dynamically loads the tenant
/// configuration using the host name from an HTTP request.
/// </summary>
public class DefaultDynamicByHostOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdServerOptions> serverOptionsAccessor,
    OpenIdServer openIdServer,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : OpenIdTenantProvider(
    templateBinderFactory,
    serverOptionsAccessor.Value,
    openIdServer,
    storeManagerFactory,
    tenantCache,
    settingSerializer,
    secretSerializer,
    secretKeyCollectionProviderFactory,
    collectionDataSourceFactory
)
{
    private Regex? DomainNameRegex { get; set; }

    private DynamicByHostOpenIdTenantOptions TenantOptions =>
        ServerOptions.Tenant.DynamicByHost ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByHost;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    protected override ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;

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
