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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using NCode.Identity;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantProviderSelector"/> abstraction.
/// </summary>
public class DefaultOpenIdTenantProviderSelector(
    IOptions<OpenIdServerOptions> serverOptionsAccessor,
    IEnumerable<IOpenIdTenantProvider> tenantProviders
) : IOpenIdTenantProviderSelector
{
    [MemberNotNullWhen(true, nameof(TenantProviderOrNull))]
    private bool TenantProviderHasValue => TenantProviderOrNull != null;

    private IOpenIdTenantProvider? TenantProviderOrNull { get; set; }

    private OpenIdServerOptions ServerOptions { get; } = serverOptionsAccessor.Value;

    private IEnumerable<IOpenIdTenantProvider> TenantProviders { get; } = tenantProviders;

    /// <inheritdoc />
    public IOpenIdTenantProvider SelectProvider(IPropertyBag propertyBag)
    {
        if (TenantProviderHasValue)
            return TenantProviderOrNull;

        var providerCode = ServerOptions.Tenant.ProviderCode;
        var tenantProvider = TenantProviders.FirstOrDefault(
            provider => string.Equals(
                providerCode,
                provider.ProviderCode,
                StringComparison.Ordinal));

        TenantProviderOrNull =
            tenantProvider ??
            throw new InvalidOperationException(
                $"Unable to find a tenant provider with code '{providerCode}'.");

        return tenantProvider;
    }
}
