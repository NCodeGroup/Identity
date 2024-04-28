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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Disposables;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that uses a static single tenant configuration.
/// </summary>
public sealed class DefaultStaticSingleOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdServerOptions> serverOptionsAccessor,
    OpenIdServer openIdServer,
    ISecretKeyProvider secretKeyProvider,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISecretSerializer secretSerializer,
    ISecretKeyProviderFactory secretKeyProviderFactory
) : OpenIdTenantProvider(
    templateBinderFactory,
    serverOptionsAccessor.Value,
    openIdServer,
    storeManagerFactory,
    tenantCache,
    secretSerializer,
    secretKeyProviderFactory
), IDisposable
{
    private ISharedReference<OpenIdTenant>? CachedTenant { get; set; }
    private ISharedReference<ISecretKeyProvider> SecretKeyProvider { get; } = SharedReference.Create(secretKeyProvider);

    private StaticSingleOpenIdTenantOptions TenantOptions =>
        ServerOptions.Tenant.StaticSingle ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.StaticSingle;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    public void Dispose()
    {
        CachedTenant?.Dispose();
        SecretKeyProvider.Dispose();
    }

    /// <inheritdoc />
    public override async ValueTask<ISharedReference<OpenIdTenant>> GetTenantAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        if (CachedTenant != null)
            return CachedTenant.AddReference();

        var tenantReference = await base.GetTenantAsync(httpContext, propertyBag, cancellationToken);
        CachedTenant = tenantReference;

        return CachedTenant.AddReference();
    }

    /// <inheritdoc />
    protected override ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
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
    protected override ValueTask<IReadOnlySettingCollection> GetTenantSettingsAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        CancellationToken cancellationToken)
    {
        // we use the same settings as the server
        return ValueTask.FromResult(OpenIdServer.Settings);
    }

    /// <inheritdoc />
    protected override ValueTask<ISharedReference<ISecretKeyProvider>> GetTenantSecretsAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        IReadOnlySettingCollection tenantSettings,
        UriDescriptor tenantBaseAddress,
        string tenantIssuer,
        CancellationToken cancellationToken)
    {
        // we use the same secrets as the server
        return ValueTask.FromResult(SecretKeyProvider.AddReference());
    }
}
