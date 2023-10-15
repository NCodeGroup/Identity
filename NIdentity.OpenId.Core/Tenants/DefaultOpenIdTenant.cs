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

using Microsoft.AspNetCore.Http.Features;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.Features;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdTenant"/> abstraction.
/// </summary>
public class DefaultOpenIdTenant : OpenIdTenant
{
    private FeatureReferences<FeatureInterfaces> _features;

    private static readonly Func<IFeatureCollection, IOpenIdTenantFeature> TenantFeatureFactory =
        _ => new DefaultOpenIdTenantFeature();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdTenant"/> class.
    /// </summary>
    /// <param name="features">The <see cref="IFeatureCollection"/> instance.</param>
    public DefaultOpenIdTenant(IFeatureCollection features)
    {
        _features.Initalize(features);
    }

    private IOpenIdTenantFeature TenantFeature =>
        _features.Fetch(ref _features.Cache.Tenant, TenantFeatureFactory) ??
        throw new InvalidOperationException();

    /// <inheritdoc />
    public override string TenantId => TenantFeature.TenantId;

    /// <inheritdoc />
    public override string DisplayName => TenantFeature.DisplayName;

    /// <inheritdoc />
    public override string Issuer => TenantFeature.Issuer;

    /// <inheritdoc />
    public override UriDescriptor BaseAddress => TenantFeature.BaseAddress;

    /// <inheritdoc />
    public override ISecretKeyProvider SecretKeyProvider => TenantFeature.SecretKeyProvider;

    /// <inheritdoc />
    public override TenantOptions Options => throw new NotImplementedException();

    private struct FeatureInterfaces
    {
        public IOpenIdTenantFeature? Tenant;
    }
}
