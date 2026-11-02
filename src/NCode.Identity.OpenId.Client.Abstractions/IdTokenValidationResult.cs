// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
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
    /// Gets the claims of the valid ID token, mapped by the JWT stack; otherwise, <see langword="null"/>.
    /// </summary>
    public ClaimsIdentity? Identity { get; init; }

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
