#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Results;

/// <summary>
/// Contains the claims about the authenticated subject returned by the <c>UserInfo</c> endpoint
/// (<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse">OpenID Connect Core 5.3.2</see>).
/// The response MUST include a <c>sub</c> member; the default claims handler supplies it and application-provided
/// enrichers contribute the remaining claims.
/// </summary>
[PublicAPI]
public sealed class UserInfoResult
{
    /// <summary>
    /// Gets or sets the claims about the authenticated subject. These are flattened into the top-level JSON response.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Claims { get; set; } = new();
}
