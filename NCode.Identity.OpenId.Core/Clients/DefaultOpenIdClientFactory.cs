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
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdClientFactory"/> abstraction.
/// </summary>
/// <param name="secretKeyCollectionFactory"></param>
public class DefaultOpenIdClientFactory(
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IOpenIdClientFactory
{
    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } = secretKeyCollectionFactory;

    /// <inheritdoc />
    public ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        IReadOnlySettingCollection settings,
        IReadOnlyCollection<SecretKey> secrets,
        IReadOnlyCollection<string> redirectUrls,
        CancellationToken cancellationToken)
    {
        var knownSettings = new ReadOnlyKnownSettingCollection(settings);
        var secretKeys = SecretKeyCollectionFactory.Create(secrets);
        var propertyBag = openIdContext.PropertyBag.Clone();

        OpenIdClient publicClient = new DefaultOpenIdClient(
            clientId,
            knownSettings,
            secretKeys,
            redirectUrls,
            propertyBag);

        return ValueTask.FromResult(publicClient);
    }

    /// <inheritdoc />
    public ValueTask<OpenIdConfidentialClient> CreateConfidentialClientAsync(
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken)
    {
        OpenIdConfidentialClient confidentialClient = new DefaultOpenIdConfidentialClient(
            publicClient,
            method,
            secretKey,
            confirmation);

        return ValueTask.FromResult(confidentialClient);
    }
}
