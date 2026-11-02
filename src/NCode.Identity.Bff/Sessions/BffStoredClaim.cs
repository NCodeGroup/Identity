// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// A claim of the session principal, as stored in the encrypted session payload.
/// </summary>
internal sealed record BffStoredClaim
{
    /// <summary>
    /// Gets the claim type.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the claim value.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the claim value type.
    /// </summary>
    public string ValueType { get; init; } = string.Empty;
}
