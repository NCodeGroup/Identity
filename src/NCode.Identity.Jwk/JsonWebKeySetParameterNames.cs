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
/// Defines the member names of a <c>JSON Web Key Set</c> (RFC 7517 §5). This is an extensible vocabulary of string
/// constants, not a closed enumeration, so a custom member can be contributed without a breaking change.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-5">RFC 7517 - JWK Set Format</seealso>
[PublicAPI]
public static class JsonWebKeySetParameterNames
{
    /// <summary>The <c>keys</c> member, the REQUIRED array of JSON Web Keys (RFC 7517 §5.1).</summary>
    public const string Keys = "keys";
}
