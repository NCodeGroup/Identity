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
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Clients;

internal class DefaultOpenIdAuthenticatedClient(
    OpenIdClient publicClient,
    string authenticationMethod,
    SecretKey secretKey,
    JsonElement? confirmation
) : OpenIdAuthenticatedClient
{
    /// <inheritdoc />
    public override string ClientId => publicClient.ClientId;

    /// <inheritdoc />
    public override bool IsDisabled => publicClient.IsDisabled;

    /// <inheritdoc />
    public override IKnownSettingCollection Settings => publicClient.Settings;

    /// <inheritdoc />
    public override ISecretKeyProvider SecretKeys => publicClient.SecretKeys;

    /// <inheritdoc />
    public override IReadOnlyCollection<Uri> RedirectUris => publicClient.RedirectUris;

    /// <inheritdoc />
    public override bool IsAuthenticated => true;

    /// <inheritdoc />
    public override OpenIdAuthenticatedClient AuthenticatedClient => this;

    /// <inheritdoc />
    public override string AuthenticationMethod { get; } = authenticationMethod;

    /// <inheritdoc />
    public override SecretKey SecretKey { get; } = secretKey;

    /// <inheritdoc />
    public override JsonElement? Confirmation { get; } = confirmation;
}
