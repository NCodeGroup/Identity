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
using NCode.Identity;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that dynamically loads the tenant
/// configuration using the host name from a HTTP request.
/// </summary>
public class DefaultDynamicByHostOpenIdTenantProvider : OpenIdTenantProvider
{
    private Regex? DomainNameRegex { get; set; }

    private DynamicByHostOpenIdTenantOptions TenantOptions =>
        ServerOptions.Tenant.DynamicByHost ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByHost;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDynamicByHostOpenIdTenantProvider"/> class.
    /// </summary>
    public DefaultDynamicByHostOpenIdTenantProvider(
        TemplateBinderFactory templateBinderFactory,
        OpenIdServerOptions serverOptions,
        IOpenIdServerSettingsProvider serverSettingsProvider,
        ITenantStore tenantStore,
        ISecretSerializer secretSerializer
    ) : base(
        templateBinderFactory,
        serverOptions,
        serverSettingsProvider,
        tenantStore,
        secretSerializer
    )
    {
        // nothing
    }

    private async ValueTask<Tenant> GetTenantByDomainAsync(string domainName, CancellationToken cancellationToken)
    {
        var tenant = await TenantStore.TryGetByDomainNameAsync(domainName, cancellationToken);
        if (tenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with domain '{domainName}' could not be found.");

        return tenant;
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

        var tenant = await GetTenantByDomainAsync(domainName, cancellationToken);
        propertyBag.Set(tenant);

        return new TenantDescriptor
        {
            TenantId = tenant.TenantId,
            DisplayName = tenant.DisplayName,
            DomainName = tenant.DomainName
        };
    }
}
