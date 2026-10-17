#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Extensions.DataProtection;

namespace NCode.Identity.Secrets.Keys;

// See the note on AkpSecretKey: ML-DSA PKCS#8 DER is [Experimental] (SYSLIB5006) but spec-fixed (RFC 9881). See ADR-0118.
#pragma warning disable SYSLIB5006

/// <summary>
/// An <see cref="AkpSecretKey"/> that holds the ML-DSA private key as <c>PKCS#8</c> DER, data-protected at rest in
/// memory. Can both sign and verify.
/// </summary>
internal sealed class DefaultAkpSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    MLDsaAlgorithm algorithm,
    ReadOnlyMemory<byte> protectedPkcs8PrivateKey,
    ReadOnlyMemory<byte> publicKey,
    ReadOnlyMemory<byte>? certificateRawData
) : AkpSecretKey(metadata, algorithm, publicKey, certificateRawData)
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedPkcs8PrivateKey { get; } = protectedPkcs8PrivateKey;

    /// <inheritdoc />
    public override bool HasPrivateKey => true;

    /// <inheritdoc />
    public override MLDsa ExportMLDsa()
    {
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        IBufferWriter<byte> writer = privateKeyBuffer;
        DataProtector.UnprotectSpan(ProtectedPkcs8PrivateKey.Span, ref writer);
        using var lease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        return MLDsa.ImportPkcs8PrivateKey(lease.Span);
    }
}
