// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Describes a registered <see cref="ISecretVerifierScheme"/> without constructing it, so the options that choose a
/// scheme by name are validated against the registered names alone as the host starts. A package contributes a
/// descriptor through <c>ICollectionDataSource&lt;SecretVerifierSchemeDescriptor&gt;</c> beside each scheme it
/// registers.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class SecretVerifierSchemeDescriptor
{
    /// <summary>
    /// Gets the name of the scheme, which is the <see cref="ISecretVerifierScheme.Name"/> of the scheme it describes.
    /// </summary>
    public required string Name { get; init; }
}
