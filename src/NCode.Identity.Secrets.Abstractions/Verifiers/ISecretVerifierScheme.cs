// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Describes a scheme that makes and reads the one-way verifiers of shared secrets. A stored verifier is the text
/// <c>$&lt;name&gt;$&lt;parameters&gt;</c>: the scheme's <see cref="Name"/>, then the parameters the scheme writes and
/// reads. A package adds a scheme by registering it, and a stored verifier is read by the scheme its text names, so a
/// verifier made with any registered scheme keeps verifying when the scheme chosen for new secrets changes. See
/// <see cref="SecretVerifierSchemes"/> for the schemes the family registers.
/// </summary>
// See ADR-1150 and ADR-1391.
[PublicAPI]
public interface ISecretVerifierScheme
{
    /// <summary>
    /// Gets the name of the scheme, which a stored verifier names first. It is a non-empty run of printable ASCII
    /// characters other than <c>$</c>, compared ordinally.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Writes the parameters of a new verifier of the specified secret: everything the stored text holds after
    /// <c>$&lt;name&gt;$</c>, as ASCII octets. The destination is a sensitive buffer that is cleared after use.
    /// </summary>
    /// <param name="secret">The octets of the secret, such as the UTF-8 bytes of a <c>client_secret</c>.</param>
    /// <param name="destination">The <see cref="IBufferWriter{T}"/> that receives the parameters.</param>
    void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination);

    /// <summary>
    /// Reads the parameters of a stored verifier whose text names this scheme.
    /// </summary>
    /// <param name="metadata">The metadata of the secret the verifier confirms.</param>
    /// <param name="parameters">The octets the stored text holds after <c>$&lt;name&gt;$</c>.</param>
    /// <param name="verifier">When this method returns <see langword="true"/>, the verifier; otherwise,
    /// <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="parameters"/> are valid parameters of this scheme;
    /// otherwise, <see langword="false"/>.</returns>
    bool TryReadParameters(
        KeyMetadata metadata,
        ReadOnlySpan<byte> parameters,
        [NotNullWhen(true)] out SecretVerifier? verifier
    );
}
