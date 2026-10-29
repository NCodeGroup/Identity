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

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents the outcome of a UserInfo request: the claims about the end-user, the protocol error the endpoint
/// reported, or the reason its response is invalid.
/// </summary>
[PublicAPI]
public sealed class UserInfoResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoResult"/> class for a valid response.
    /// </summary>
    /// <param name="claims">The JSON object of the claims.</param>
    /// <param name="identity">The claims, mapped by the JWT stack.</param>
    public UserInfoResult(JsonElement claims, ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Claims = claims;
        Identity = identity;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoResult"/> class for an error the endpoint reported.
    /// </summary>
    /// <param name="error">The protocol error.</param>
    public UserInfoResult(ProtocolError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoResult"/> class for an invalid response.
    /// </summary>
    /// <param name="failureCode">The machine-readable code of the reason, a <see cref="UserInfoFailureCodes"/> value,
    /// or a <see cref="TokenValidationFailureCodes"/> value for a signed or encrypted response that fails its
    /// validation.</param>
    /// <param name="failureReason">The reason the response is invalid.</param>
    public UserInfoResult(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);
        ArgumentException.ThrowIfNullOrEmpty(failureReason);
        FailureCode = failureCode;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the JSON object of the claims when the response is valid, the payload of a signed response; otherwise,
    /// <see langword="default"/>.
    /// </summary>
    public JsonElement Claims { get; }

    /// <summary>
    /// Gets the claims when the response is valid, mapped by the JWT stack; otherwise, <see langword="null"/>.
    /// </summary>
    public ClaimsIdentity? Identity { get; }

    /// <summary>
    /// Gets the protocol error the endpoint reported; otherwise, <see langword="null"/>.
    /// </summary>
    public ProtocolError? Error { get; }

    /// <summary>
    /// Gets the machine-readable code of the reason the response is invalid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the reason the response is invalid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the response is valid.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Identity))]
    public bool IsSuccess => Identity is not null;
}
