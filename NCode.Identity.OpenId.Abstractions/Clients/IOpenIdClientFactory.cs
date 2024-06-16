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
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;

namespace NCode.Identity.OpenId.Clients;

[PublicAPI]
public interface IOpenIdClientFactory
{
    ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        IReadOnlySettingCollection settings,
        IReadOnlyCollection<SecretKey> secrets,
        IReadOnlyCollection<string> redirectUrls,
        CancellationToken cancellationToken);

    ValueTask<OpenIdConfidentialClient> CreateConfidentialClientAsync(
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken);
}
