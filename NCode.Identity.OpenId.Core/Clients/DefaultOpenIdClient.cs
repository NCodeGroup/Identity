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

using NCode.Identity.OpenId.DataContracts;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Settings;
using NCode.Jose.SecretKeys;

namespace NCode.Identity.OpenId.Clients;

internal class DefaultOpenIdClient(
    ISecretKeyCollectionFactory secretKeyCollectionFactory,
    ISecretSerializer secretSerializer,
    OpenIdContext openIdContext,
    Client client
) : OpenIdClient
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } = secretKeyCollectionFactory;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private OpenIdContext OpenIdContext { get; } = openIdContext;
    private Client ClientModel { get; } = client;

    private KnownSettingCollection? SettingsOrNull { get; set; }
    private ISecretKeyCollection? SecretKeysOrNull { get; set; }

    /// <inheritdoc />
    public override string ClientId => ClientModel.ClientId;

    /// <inheritdoc />
    public override bool IsDisabled => ClientModel.IsDisabled;

    /// <inheritdoc />
    public override IKnownSettingCollection Settings => SettingsOrNull ??= LoadSettings();

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys => SecretKeysOrNull ??= LoadSecretKeys();

    /// <inheritdoc />
    public override IReadOnlyCollection<Uri> RedirectUris => ClientModel.RedirectUris;

    /// <inheritdoc />
    public override bool IsAuthenticated => false;

    /// <inheritdoc />
    public override OpenIdAuthenticatedClient? AuthenticatedClient => null;

    private KnownSettingCollection LoadSettings()
    {
        // TODO: use a factory
        return new KnownSettingCollection(OpenIdContext.Tenant.Settings.Merge(ClientModel.Settings));
    }

    private ISecretKeyCollection LoadSecretKeys() =>
        SecretKeyCollectionFactory.Create(
            SecretSerializer.DeserializeSecrets(
                ClientModel.Secrets,
                out _));
}
