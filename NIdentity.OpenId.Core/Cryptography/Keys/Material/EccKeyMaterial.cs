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

namespace NIdentity.OpenId.Cryptography.Keys.Material;

/// <summary>
/// Provides an implementation of <see cref="KeyMaterial"/> that uses key material from an <c>Elliptic-Curve</c>.
/// </summary>
/// <remarks>
/// Both <see cref="ECDiffieHellman"/> and <see cref="ECDsa"/> can be used interchangeable to to export <c>Elliptic-Curve</c> key material.
/// </remarks>
public class EccKeyMaterial : KeyMaterial
{
    private ECDiffieHellman EcDiffieHellman { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EccKeyMaterial"/> class with the specified <c>Elliptic-Curve</c> key material.
    /// </summary>
    /// <param name="ecDiffieHellman">The <see cref="ECDiffieHellman"/> that contains the key material.</param>
    public EccKeyMaterial(ECDiffieHellman ecDiffieHellman) =>
        EcDiffieHellman = ecDiffieHellman;

    /// <inheritdoc />
    public override void Dispose()
    {
        EcDiffieHellman.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override bool TryExportKey(Span<byte> destination, out int bytesWritten) =>
        EcDiffieHellman.TryExportPkcs8PrivateKey(destination, out bytesWritten);
}
