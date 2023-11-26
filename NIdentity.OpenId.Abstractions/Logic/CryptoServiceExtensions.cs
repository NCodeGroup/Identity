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

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Provides extension methods for the <see cref="ICryptoService"/> abstraction.
/// </summary>
public static class CryptoServiceExtensions
{
    private const int DefaultKeyByteLength = 32;

    /// <summary>
    /// Generates a strong cryptographic random 256-bit value that is Base64Url encoded.
    /// </summary>
    /// <param name="cryptoService">The <see cref="ICryptoService"/> instance.</param>
    /// <param name="byteLength">Specifies the number of random bytes to generate. Defaults to 32 bytes.</param>
    /// <returns>The newly generated random bytes encoded as a string.</returns>
    public static string GenerateUrlSafeKey(this ICryptoService cryptoService, int byteLength = DefaultKeyByteLength) =>
        cryptoService.GenerateKey(byteLength, BinaryEncodingType.Base64Url);
}
