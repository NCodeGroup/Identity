// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    /// Gets the device verification code.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.DeviceCode)]
    public required string DeviceCode { get; init; }

    /// <summary>
    /// Gets the end-user verification code.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.UserCode)]
    public required string UserCode { get; init; }

    /// <summary>
    /// Gets the end-user verification URI on the authorization server.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.VerificationUri)]
    public required string VerificationUri { get; init; }

    /// <summary>
    /// Gets the verification URI that includes the <c>user_code</c>, designed for non-textual transmission.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.VerificationUriComplete)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerificationUriComplete { get; init; }

    /// <summary>
    /// Gets the lifetime in seconds of the <c>device_code</c> and <c>user_code</c>.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.ExpiresIn)]
    public required int ExpiresIn { get; init; }

    /// <summary>
    /// Gets the minimum amount of time in seconds that the client should wait between polling requests to the
    /// token endpoint.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.Interval)]
    public required int Interval { get; init; }
}
