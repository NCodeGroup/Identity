// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Protects the supplied raw private-key bytes and returns an opaque protected representation. Supplied to a
/// <see cref="ISecretKeyTypeDescriptor"/> so it can protect its freshly-generated key material without taking a
/// dependency on the protection mechanism (for example data protection).
/// </summary>
/// <param name="privateKeyBytes">The sensitive raw private-key bytes to protect.</param>
/// <returns>An opaque protected representation of the private key.</returns>
[PublicAPI]
public delegate string SecretKeyMaterialProtector(ReadOnlySpan<byte> privateKeyBytes);
