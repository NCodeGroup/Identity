// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes a single remote key provider the server registers, so that management clients can offer the providers a
/// tenant's signing keys can be created or bound in from server discovery rather than hard-coded assumptions.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RemoteKeyProviderCapabilityResource
{
    /// <summary>
    /// Gets the name of the remote key provider, such as <c>aws-kms</c>.
    /// </summary>
    public required string Name { get; init; }
}
