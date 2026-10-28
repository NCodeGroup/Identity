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

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the values of the <c>use</c> member of a <c>JSON Web Key</c> (RFC 7517 §4.2). This is an extensible
/// vocabulary of string constants, not a closed enumeration, so a custom use can be contributed without a breaking
/// change.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.2">RFC 7517 - The "use" (Public Key Use) Parameter</seealso>
[PublicAPI]
public static class JsonWebKeyUses
{
    /// <summary>The <c>sig</c> value, indicating the key is used to verify or create signatures (RFC 7517 §4.2).</summary>
    public const string Signature = "sig";

    /// <summary>The <c>enc</c> value, indicating the key is used to encrypt or decrypt data (RFC 7517 §4.2).</summary>
    public const string Encryption = "enc";
}
