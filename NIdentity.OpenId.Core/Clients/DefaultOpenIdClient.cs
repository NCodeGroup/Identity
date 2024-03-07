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

using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Clients;

internal class DefaultOpenIdClient(
    ISecretSerializer secretSerializer,
    ISecretKeyProviderFactory secretKeyProviderFactory,
    OpenIdContext context,
    Client client
) : OpenIdClient
{
    private KnownSettingCollection? SettingsOrNull { get; set; }
    private ISecretKeyProvider? SecretKeysOrNull { get; set; }

    /// <inheritdoc />
    public override string ClientId => client.ClientId;

    /// <inheritdoc />
    public override bool IsDisabled => client.IsDisabled;

    /// <inheritdoc />
    public override IKnownSettingCollection Settings => SettingsOrNull ??= LoadSettings();

    /// <inheritdoc />
    public override ISecretKeyProvider SecretKeys => SecretKeysOrNull ??= LoadSecretKeys();

    /// <inheritdoc />
    public override IReadOnlyCollection<Uri> RedirectUris => client.RedirectUris;

    /// <inheritdoc />
    public override bool IsAuthenticated => false;

    /// <inheritdoc />
    public override OpenIdAuthenticatedClient? AuthenticatedClient => null;

    private KnownSettingCollection LoadSettings() =>
        new(new SettingCollection(client.Settings));

    private ISecretKeyProvider LoadSecretKeys()
    {
        // TODO: add support for a dynamic data source that re-fetches secrets from the store
        var secretKeys = secretSerializer.DeserializeSecrets(client.Secrets);
        try
        {
            var provider = secretKeyProviderFactory.CreateStatic(secretKeys);
            context.Http.Response.RegisterForDispose(provider);
            return provider;
        }
        catch
        {
            secretKeys.DisposeAll();
            throw;
        }
    }
}
