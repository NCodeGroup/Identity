#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdClient"/> abstraction.
/// </summary>
public class DefaultOpenIdClient(
    string clientId,
    IReadOnlySettingCollection settings,
    ISecretKeyCollection secretKeys,
    IReadOnlyCollection<string> redirectUrls,
    IPropertyBag propertyBag
) : OpenIdClient
{
    /// <inheritdoc />
    public override string ClientId { get; } = clientId;

    /// <inheritdoc />
    public override IReadOnlySettingCollection Settings { get; } = settings;

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys { get; } = secretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<string> RedirectUrls { get; } = redirectUrls;

    /// <inheritdoc />
    public override bool IsConfidential => false;

    /// <inheritdoc />
    public override OpenIdConfidentialClient? ConfidentialClient => null;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;
}
