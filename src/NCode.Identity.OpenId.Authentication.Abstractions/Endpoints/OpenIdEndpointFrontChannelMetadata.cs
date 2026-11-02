// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdEndpointFrontChannelMetadata"/> abstraction.
/// </summary>
[PublicAPI]
public sealed class OpenIdEndpointFrontChannelMetadata : IOpenIdEndpointFrontChannelMetadata
{
    /// <inheritdoc />
    public bool IsFrontChannel { get; init; } = true;
}
