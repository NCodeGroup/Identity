// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Represents a validation failure produced by a management operation. It carries the HTTP status code and a
/// fixed, safe detail message that is returned to the caller (never a raw exception message).
/// </summary>
[PublicAPI]
public sealed class ManagementError
{
    /// <summary>
    /// Gets the HTTP status code that describes the failure.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the fixed, safe detail message describing the failure.
    /// </summary>
    public required string Detail { get; init; }
}
