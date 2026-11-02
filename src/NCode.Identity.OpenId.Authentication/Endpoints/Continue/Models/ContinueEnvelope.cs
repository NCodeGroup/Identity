// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;

/// <summary>
/// Contains the properties that will be persisted in order to continue an operation.
/// </summary>
public readonly record struct ContinueEnvelope
{
    /// <summary>
    /// Gets the <see cref="string"/> code that contains the type of operation to continue.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the <see cref="JsonElement"/> payload that contains any required data to continue the operation.
    /// </summary>
    public required JsonElement PayloadJson { get; init; }
}
