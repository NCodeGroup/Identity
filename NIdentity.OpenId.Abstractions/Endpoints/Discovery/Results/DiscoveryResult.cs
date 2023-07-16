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

using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Discovery.Results;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization server metadata discovery response.
/// </summary>
public class DiscoveryResult : OpenIdResult
{
    /// <summary>
    /// Gets the metadata collection for an <c>OAuth</c> or<c>OpenId Connect</c> authorization server.
    /// </summary>
    public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override async ValueTask ExecuteResultAsync(OpenIdEndpointContext context) =>
        await GetExecutor<DiscoveryResult>(context).ExecuteResultAsync(context, this);
}
