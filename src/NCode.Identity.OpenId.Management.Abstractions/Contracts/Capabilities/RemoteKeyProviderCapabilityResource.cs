// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes a single remote key provider the server registers, so that management clients can offer the providers,
/// and the algorithms each holds keys for, from server discovery rather than hard-coded assumptions.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RemoteKeyProviderCapabilityResource
{
    /// <summary>
    /// Gets the name of the remote key provider, such as <c>aws-kms</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the signing algorithms the provider holds keys for that the deployment accepts, each with its key's type,
    /// size and curve.
    /// </summary>
    public required IReadOnlyList<RemoteKeyAlgorithmCapabilityResource> Algorithms { get; init; }
}
