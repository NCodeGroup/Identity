// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Provides the marker of an endpoint whose request or response body carries credentials or personal data, so HTTP
/// logging never writes either body.
/// </summary>
internal sealed class ConfidentialBodyMetadata
{
    /// <summary>
    /// Gets the singleton instance of <see cref="ConfidentialBodyMetadata"/>.
    /// </summary>
    public static ConfidentialBodyMetadata Instance { get; } = new();
}
