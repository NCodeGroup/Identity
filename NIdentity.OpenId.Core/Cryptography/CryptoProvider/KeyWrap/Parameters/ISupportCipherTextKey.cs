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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;

/// <summary>
/// Specifies that the current instance supports cipher text key material.
/// </summary>
public interface ISupportCipherTextKey
{
    /// <summary>
    /// Gets the cipher text key material for the current instance.
    /// </summary>
    ReadOnlyMemory<byte> CipherTextKey { get; }
}
