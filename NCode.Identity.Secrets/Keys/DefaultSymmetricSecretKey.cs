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

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides a default implementation for the <see cref="SymmetricSecretKey"/> abstraction.
/// </summary>
public class DefaultSymmetricSecretKey(
    IDataProtector dataProtector,
    KeyMetadata metadata,
    int keySizeBytes,
    ReadOnlyMemory<byte> protectedPrivateKey
) : SymmetricSecretKey
{
    private IDataProtector DataProtector { get; } = dataProtector;
    private ReadOnlyMemory<byte> ProtectedPrivateKey { get; } = protectedPrivateKey;

    /// <inheritdoc />
    public override KeyMetadata Metadata { get; } = metadata;

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes { get; } = keySizeBytes;

    /// <inheritdoc />
    public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
        DataProtector.UnprotectSpan(ProtectedPrivateKey.Span, ref destination);
}
