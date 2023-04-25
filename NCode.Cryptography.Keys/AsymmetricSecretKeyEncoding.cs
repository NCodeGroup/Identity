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

namespace NCode.Cryptography.Keys;

/// <summary>
/// Specifies how an <see cref="AsymmetricSecretKey"/> is encoded to/from binary data.
/// </summary>
public enum AsymmetricSecretKeyEncoding
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies to use <c>Privacy-Enhanced Mail</c> rules for encoding binary data.
    /// </summary>
    Pem,

    /// <summary>
    /// Specifies to use the <c>PKCS #8</c> specification for encoding binary data.
    /// </summary>
    Pkcs8
}
