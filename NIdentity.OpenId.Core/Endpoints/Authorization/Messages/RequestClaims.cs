#region Copyright Preamble
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

using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class RequestClaims : IRequestClaims
{
    [JsonPropertyName(OpenIdConstants.Parameters.UserInfo)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestClaimDictionary? UserInfo { get; set; }

    [JsonPropertyName(OpenIdConstants.Parameters.IdToken)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequestClaimDictionary? IdToken { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = new();

    IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.UserInfo => UserInfo;

    IReadOnlyDictionary<string, IRequestClaim?>? IRequestClaims.IdToken => IdToken;
}