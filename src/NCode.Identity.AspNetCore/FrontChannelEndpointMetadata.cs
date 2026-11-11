// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the default implementation of the <see cref="IFrontChannelEndpointMetadata"/> abstraction.
/// </summary>
[PublicAPI]
public sealed class FrontChannelEndpointMetadata : IFrontChannelEndpointMetadata
{
    /// <inheritdoc />
    public bool IsFrontChannel { get; init; } = true;
}
