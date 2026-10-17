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

using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Specifies the wire serialization used to represent a JOSE (JWS or JWE) token.
/// </summary>
[PublicAPI]
public enum JoseSerializationFormat
{
    /// <summary>
    /// The URL-safe, dot-separated compact serialization defined by <c>RFC 7515</c> §7.1 (JWS) and
    /// <c>RFC 7516</c> §7.1 (JWE). This representation supports exactly one signature/recipient and cannot
    /// carry unprotected header members.
    /// </summary>
    Compact = 0,

    /// <summary>
    /// The flattened JSON serialization defined by <c>RFC 7515</c> §7.2.2 (JWS) and <c>RFC 7516</c> §7.2.2 (JWE).
    /// This representation is optimized for the single-signature/single-recipient case and may carry unprotected
    /// header members.
    /// </summary>
    Flattened = 1,

    /// <summary>
    /// The general JSON serialization defined by <c>RFC 7515</c> §7.2.1 (JWS) and <c>RFC 7516</c> §7.2.1 (JWE).
    /// This representation supports multiple signatures (JWS) / multiple recipients (JWE) and may carry
    /// unprotected header members.
    /// </summary>
    General = 2,
}
