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

namespace NIdentity.OpenId.Cryptography.Keys.Material;

/// <summary>
/// Provides an implementation of <see cref="KeyMaterial"/> that uses key material from an <see cref="AsymmetricAlgorithm"/>.
/// </summary>
public class AsymmetricKeyMaterial : KeyMaterial
{
    private AsymmetricAlgorithm AsymmetricAlgorithm { get; }

    /// <inheritdoc />
    public override int KeySizeBits => AsymmetricAlgorithm.KeySize;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsymmetricKeyMaterial"/> class with the specified <see cref="AsymmetricAlgorithm"/>.
    /// </summary>
    /// <param name="asymmetricAlgorithm">The <see cref="AsymmetricAlgorithm"/> that contains the key material.</param>
    public AsymmetricKeyMaterial(AsymmetricAlgorithm asymmetricAlgorithm) =>
        AsymmetricAlgorithm = asymmetricAlgorithm;

    /// <inheritdoc />
    public override void Dispose()
    {
        AsymmetricAlgorithm.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override bool TryExportKey(Span<byte> destination, out int bytesWritten) =>
        AsymmetricAlgorithm.TryExportPkcs8PrivateKey(destination, out bytesWritten);
}