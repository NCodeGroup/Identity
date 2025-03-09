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
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides a factory abstraction for creating instances of <see cref="OpenIdClient"/> and <see cref="OpenIdConfidentialClient"/>.
/// </summary>
[PublicAPI]
public interface IOpenIdClientFactory
{
    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdClient"/> that represents a public client.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The <see cref="string"/> that contains the client identifier.</param>
    /// <param name="settings">The <see cref="IReadOnlySettingCollection"/> that contains the client settings.</param>
    /// <param name="secrets">The collection of <see cref="SecretKey"/> instances that contains the secrets only known to the client.</param>
    /// <param name="redirectUrls">The collection of <see cref="string"/> instances that contains the redirect URLs that the client may use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation,
    /// containing the <see cref="OpenIdClient"/> that represents a public client.</returns>
    ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        IReadOnlySettingCollection settings,
        IReadOnlyCollection<SecretKey> secrets,
        IReadOnlyCollection<string> redirectUrls,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdConfidentialClient"/> that represents a confidential client.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="publicClient">The <see cref="OpenIdClient"/> that represents the public client.</param>
    /// <param name="method">The <see cref="string"/> that contains the client authentication method.</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> instance that contains the secret how the client was authenticated.</param>
    /// <param name="confirmation">The <see cref="JsonElement"/> that contains the client confirmation, if any.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation,
    /// containing the <see cref="OpenIdConfidentialClient"/> that represents a confidential client.</returns>
    ValueTask<OpenIdConfidentialClient> CreateConfidentialClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken
    );
}
