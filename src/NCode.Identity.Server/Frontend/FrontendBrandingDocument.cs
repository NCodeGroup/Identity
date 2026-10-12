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

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// The serializable branding tokens embedded in the frontend bootstrap document.
/// </summary>
internal sealed record FrontendBrandingDocument
{
    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    [JsonPropertyName("logoUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogoUrl { get; init; }

    [JsonPropertyName("primaryColor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrimaryColor { get; init; }

    [JsonPropertyName("secondaryColor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondaryColor { get; init; }

    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }
}
