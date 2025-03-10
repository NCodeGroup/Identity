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

using NCode.Disposables;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdTenant"/> abstraction.
/// </summary>
public class DefaultOpenIdTenant(
    TenantDescriptor tenantDescriptor,
    string issuer,
    UriDescriptor baseAddress,
    AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> settingsReference,
    AsyncSharedReferenceLease<ISecretKeyCollectionProvider> secretsReference,
    IPropertyBag propertyBag
) : OpenIdTenant
{
    private TenantDescriptor TenantDescriptor { get; } = tenantDescriptor;
    private AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> SettingsReference { get; set; } = settingsReference.AddReference();
    private AsyncSharedReferenceLease<ISecretKeyCollectionProvider> SecretsReference { get; set; } = secretsReference.AddReference();

    /// <inheritdoc />
    public override string TenantId => TenantDescriptor.TenantId;

    /// <inheritdoc />
    public override string DisplayName => TenantDescriptor.DisplayName;

    /// <inheritdoc />
    public override string Issuer { get; } = issuer;

    /// <inheritdoc />
    public override UriDescriptor BaseAddress { get; } = baseAddress;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider => SettingsReference.Value;

    /// <inheritdoc />
    public override ISecretKeyCollectionProvider SecretsProvider => SecretsReference.Value;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await SettingsReference.DisposeAsync();
        SettingsReference = default;

        await SecretsReference.DisposeAsync();
        SecretsReference = default;
    }
}
