// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Provides a scheme a package registers: its parameters are the secret itself, which only a test would store.
/// </summary>
internal sealed class PlainTextSecretVerifierScheme : ISecretVerifierScheme
{
    public const string SchemeName = "test-plain";

    public string Name => SchemeName;

    public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination) =>
        destination.Write(secret);

    public bool TryReadParameters(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    )
    {
        verifier = new PlainTextSecretVerifier(metadata, parameters.ToArray());
        return true;
    }
}
