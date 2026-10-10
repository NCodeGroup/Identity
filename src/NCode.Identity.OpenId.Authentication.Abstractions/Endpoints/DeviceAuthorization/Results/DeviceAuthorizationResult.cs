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

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;

/// <summary>
/// Contains the parameters for a device authorization response (RFC 8628 §3.2).
/// </summary>
[PublicAPI]
public sealed class DeviceAuthorizationResult
{
    /// <summary>
    /// Gets or sets the device verification code.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.DeviceCode)]
    public required string DeviceCode { get; init; }

    /// <summary>
    /// Gets or sets the end-user verification code.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.UserCode)]
    public required string UserCode { get; init; }

    /// <summary>
    /// Gets or sets the end-user verification URI on the authorization server.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.VerificationUri)]
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Gets or sets the verification URI that includes the <c>user_code</c>, designed for non-textual transmission.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.VerificationUriComplete)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerificationUriComplete { get; init; }

    /// <summary>
    /// Gets or sets the lifetime in seconds of the <c>device_code</c> and <c>user_code</c>.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.ExpiresIn)]
    public required int ExpiresIn { get; init; }

    /// <summary>
    /// Gets or sets the minimum amount of time in seconds that the client should wait between polling requests to the
    /// token endpoint.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.Interval)]
    public required int Interval { get; init; }
}
