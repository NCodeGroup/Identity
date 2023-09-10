#region Copyright Preamble

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
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.Signature;

/// <summary>
/// Provides an implementation of <see cref="SignatureAlgorithm"/> that doesn't calculate any digital signatures.
/// </summary>
public class NoneSignatureAlgorithm : SignatureAlgorithm
{
    /// <inheritdoc />
    public override string Code => AlgorithmCodes.DigitalSignature.None;

    /// <inheritdoc />
    public override Type KeyType => typeof(SecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => Array.Empty<KeySizes>();

    /// <inheritdoc />
    public override int GetSignatureSizeBytes(int keySizeBits) => 0;

    /// <inheritdoc />
    public override bool TrySign(SecretKey secretKey, ReadOnlySpan<byte> inputData, Span<byte> signature, out int bytesWritten)
    {
        bytesWritten = 0;
        return true;
    }
}
