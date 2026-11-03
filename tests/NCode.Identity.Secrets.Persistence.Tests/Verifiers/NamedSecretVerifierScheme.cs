// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides a scheme that has only a name, for a test of how a catalog indexes schemes; it neither writes nor reads.
/// </summary>
internal sealed class NamedSecretVerifierScheme(string name) : ISecretVerifierScheme
{
    public string Name { get; } = name;

    public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination) =>
        throw new NotSupportedException();

    public bool TryReadParameters(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    ) => throw new NotSupportedException();
}
