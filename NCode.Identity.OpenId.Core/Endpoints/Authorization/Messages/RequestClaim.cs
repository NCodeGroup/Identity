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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IRequestClaim"/> abstraction.
/// </summary>
[PublicAPI]
public class RequestClaim : IRequestClaim
{
    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Essential)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Essential { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Value)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    /// <inheritdoc />
    [JsonPropertyName(OpenIdConstants.Parameters.Values)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Values { get; set; }

    /// <summary>
    /// Contains the additional data that is not captured by other properties during serialization.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = new();
}
