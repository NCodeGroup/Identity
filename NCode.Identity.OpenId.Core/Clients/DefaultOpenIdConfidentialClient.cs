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

using System.Text.Json;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdConfidentialClient"/> abstraction.
/// </summary>
public class DefaultOpenIdConfidentialClient(
    OpenIdClient publicClient,
    string authenticationMethod,
    SecretKey secretKey,
    JsonElement? confirmation
) : OpenIdConfidentialClient
{
    private OpenIdClient PublicClient { get; } = publicClient;

    /// <inheritdoc />
    public override string ClientId => PublicClient.ClientId;

    /// <inheritdoc />
    public override IReadOnlyKnownSettingCollection Settings => PublicClient.Settings;

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys => PublicClient.SecretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<string> RedirectUrls => PublicClient.RedirectUrls;

    /// <inheritdoc />
    public override bool IsConfidential => true;

    /// <inheritdoc />
    public override OpenIdConfidentialClient ConfidentialClient => this;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag => PublicClient.PropertyBag;

    /// <inheritdoc />
    public override string AuthenticationMethod { get; } = authenticationMethod;

    /// <inheritdoc />
    public override SecretKey SecretKey { get; } = secretKey;

    /// <inheritdoc />
    public override JsonElement? Confirmation { get; } = confirmation;
}
