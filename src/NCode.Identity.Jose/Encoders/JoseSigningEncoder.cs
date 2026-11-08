// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;

namespace NCode.Identity.Jose.Encoders;

/// <summary>
/// Provides an implementation of <see cref="JoseEncoder"/> that can be used to sign JWS tokens.
/// </summary>
/// <param name="joseSerializer">The <see cref="JoseSerializer"/> instance.</param>
/// <param name="signingOptions">The JOSE signing credentials and options.</param>
internal class JoseSigningEncoder(JoseSerializer joseSerializer, JoseSigningOptions signingOptions)
    : CommonJoseEncoder(joseSerializer)
{
    private JoseSigningOptions SigningOptions { get; } = signingOptions;

    /// <inheritdoc />
    protected override JsonSerializerOptions? PayloadJsonOptions => SigningOptions.JsonOptions;

    /// <inheritdoc />
    public override ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    ) => JoseSerializer.EncodeAsync(tokenWriter, payload, SigningOptions, cancellationToken);
}
