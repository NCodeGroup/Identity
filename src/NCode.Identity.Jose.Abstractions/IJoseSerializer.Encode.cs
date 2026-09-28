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

namespace NCode.Identity.Jose;

partial interface IJoseSerializer
{
    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified signing credentials and options.
    /// The returned encoder is the single entry point for encoding JWS tokens.
    /// </summary>
    /// <param name="signingOptions">The JOSE signing credentials and options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(JoseSigningOptions signingOptions);

    /// <summary>
    /// Creates a new <see cref="JoseEncoder"/> with the specified encrypting credentials and options.
    /// The returned encoder is the single entry point for encrypting JWE tokens.
    /// </summary>
    /// <param name="encryptionOptions">The JOSE encryption credentials and options.</param>
    /// <returns>The newly created <see cref="JoseEncoder"/> instance.</returns>
    JoseEncoder CreateEncoder(JoseEncryptionOptions encryptionOptions);
}
