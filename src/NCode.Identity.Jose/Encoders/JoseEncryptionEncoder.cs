// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an implementation of <see cref="JoseEncoder"/> that can be used to encrypt JWE tokens.
/// </summary>
/// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
/// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
internal class JoseEncryptionEncoder(
    JoseSerializer joseSerializer,
    JoseEncryptionOptions encryptionOptions
) : CommonJoseEncoder(joseSerializer)
{
    private JoseEncryptionOptions EncryptionOptions { get; } = encryptionOptions;

    /// <inheritdoc />
    protected override JsonSerializerOptions? PayloadJsonOptions => EncryptionOptions.JsonOptions;

    /// <inheritdoc />
    public override void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<byte> payload) =>
        JoseSerializer.Encode(tokenWriter, payload, EncryptionOptions);
}
