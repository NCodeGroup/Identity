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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdServer"/> abstraction.
/// </summary>
public class DefaultOpenIdServer(
    IReadOnlySettingCollectionProvider settingsProvider,
    ISecretKeyCollectionProvider secretsProvider,
    IPropertyBag propertyBag
) : OpenIdServer, IAsyncDisposable
{
    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider { get; } = settingsProvider;

    /// <inheritdoc />
    public override ISecretKeyCollectionProvider SecretsProvider { get; } = secretsProvider;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await SecretsProvider.DisposeAsync();
        await SettingsProvider.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
