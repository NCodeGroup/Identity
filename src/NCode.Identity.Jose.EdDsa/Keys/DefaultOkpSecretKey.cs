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

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Keys;

/// <summary>
/// An <see cref="OkpSecretKey"/> that holds the private seed, data-protected at rest in memory. Can both sign and
/// verify.
/// </summary>
internal sealed class DefaultOkpSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    OkpCurveInfo curve,
    ReadOnlyMemory<byte> protectedSeed,
    ReadOnlyMemory<byte> publicKey
) : OkpSecretKey(metadata, curve.Name, curve.KeySizeBits, publicKey, certificateRawData: null)
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedSeed { get; } = protectedSeed;

    /// <inheritdoc />
    public override bool HasPrivateKey => true;

    /// <inheritdoc />
    public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
        DataProtector.UnprotectSpan(ProtectedSeed.Span, ref destination);
}
