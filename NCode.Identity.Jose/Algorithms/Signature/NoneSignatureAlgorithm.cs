﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose.Algorithms.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that doesn't calculate any digital signatures.
/// </summary>
[PublicAPI]
public class NoneSignatureAlgorithm : SignatureAlgorithm
{
    /// <summary>
    /// Gets a singleton instance of <see cref="NoneSignatureAlgorithm"/>.
    /// </summary>
    public static NoneSignatureAlgorithm Singleton { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NoneSignatureAlgorithm"/> class.
    /// </summary>
    private NoneSignatureAlgorithm()
    {
        // nothing
    }

    /// <inheritdoc />
    public override string Code => AlgorithmCodes.DigitalSignature.None;

    /// <inheritdoc />
    public override Type KeyType => typeof(SecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => Array.Empty<KeySizes>();

    /// <inheritdoc />
    public override HashAlgorithmName HashAlgorithmName => default;

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => 0;

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        bytesWritten = 0;
        return true;
    }

    /// <inheritdoc />
    public override bool Verify(SecretKey secretKey, ReadOnlySpan<byte> inputData, ReadOnlySpan<byte> signature)
    {
        return signature.Length == 0;
    }
}
