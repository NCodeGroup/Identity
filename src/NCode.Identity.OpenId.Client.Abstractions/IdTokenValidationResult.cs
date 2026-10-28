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
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents the outcome of validating an ID token: either its verified payload or the reason it is invalid.
/// </summary>
[PublicAPI]
public sealed class IdTokenValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdTokenValidationResult"/> class for a valid ID token.
    /// </summary>
    /// <param name="payload">The verified payload of the ID token.</param>
    public IdTokenValidationResult(JsonElement payload)
    {
        Payload = payload;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdTokenValidationResult"/> class for an invalid ID token.
    /// </summary>
    /// <param name="failureCode">The machine-readable code of the reason the ID token is invalid.</param>
    /// <param name="failureReason">The reason the ID token is invalid.</param>
    public IdTokenValidationResult(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);
        ArgumentException.ThrowIfNullOrEmpty(failureReason);
        FailureCode = failureCode;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the verified payload when the ID token is valid; otherwise, <see langword="default"/>.
    /// </summary>
    public JsonElement Payload { get; }

    /// <summary>
    /// Gets the machine-readable code of the reason the ID token is invalid, a <see cref="TokenValidationFailureCodes"/>
    /// value or another code a validator reports; otherwise, <see langword="null"/>.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the reason the ID token is invalid; otherwise, <see langword="null"/>.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the ID token is valid.
    /// </summary>
    [MemberNotNullWhen(false, nameof(FailureCode), nameof(FailureReason))]
    public bool IsValid => FailureCode is null;
}
