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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Provides the <see cref="JsonWebKeyTypeDescriptor"/> of each key type registered in the IANA JSON Web Key Types
/// registry that this library knows. A contributed key type supplies its own descriptor.
/// </summary>
[PublicAPI]
public static class JsonWebKeyTypeDescriptors
{
    /// <summary>
    /// Gets the descriptor of the <c>RSA</c> key type (RFC 7518 §6.3).
    /// </summary>
    public static JsonWebKeyTypeDescriptor Rsa { get; } =
        new(
            JsonWebKeyTypes.Rsa,
            [JsonWebKeyParameterNames.Exponent, JsonWebKeyParameterNames.Modulus],
            [JsonWebKeyParameterNames.EccPrivateKey, "p", "q", "dp", "dq", "qi", "oth"]
        );

    /// <summary>
    /// Gets the descriptor of the <c>EC</c> key type (RFC 7518 §6.2).
    /// </summary>
    public static JsonWebKeyTypeDescriptor Ec { get; } =
        new(
            JsonWebKeyTypes.Ec,
            [
                JsonWebKeyParameterNames.Curve,
                JsonWebKeyParameterNames.X,
                JsonWebKeyParameterNames.Y,
            ],
            [JsonWebKeyParameterNames.EccPrivateKey]
        );

    /// <summary>
    /// Gets the descriptor of the <c>OKP</c> key type (RFC 8037).
    /// </summary>
    public static JsonWebKeyTypeDescriptor Okp { get; } =
        new(
            JsonWebKeyTypes.Okp,
            [JsonWebKeyParameterNames.Curve, JsonWebKeyParameterNames.X],
            [JsonWebKeyParameterNames.EccPrivateKey]
        );

    /// <summary>
    /// Gets the descriptor of the <c>oct</c> key type (RFC 7518 §6.4).
    /// </summary>
    public static JsonWebKeyTypeDescriptor Oct { get; } =
        new(
            JsonWebKeyTypes.Oct,
            [JsonWebKeyParameterNames.SymmetricKey],
            [JsonWebKeyParameterNames.SymmetricKey]
        );

    /// <summary>
    /// Gets the descriptor of the <c>AKP</c> key type (RFC 9964 §6).
    /// </summary>
    public static JsonWebKeyTypeDescriptor Akp { get; } =
        new(
            JsonWebKeyTypes.Akp,
            [JsonWebKeyParameterNames.Algorithm, JsonWebKeyParameterNames.PublicKey],
            [JsonWebKeyParameterNames.PrivateKey]
        );

    private static readonly FrozenDictionary<string, JsonWebKeyTypeDescriptor> StandardByName =
        new[] { Rsa, Ec, Okp, Oct, Akp }.ToFrozenDictionary(
            descriptor => descriptor.Name,
            StringComparer.Ordinal
        );

    /// <summary>
    /// Gets the union of the private members of every standard key type.
    /// </summary>
    internal static FrozenSet<string> StandardPrivateMembers { get; } =
        new[] { Rsa, Ec, Okp, Oct, Akp }
            .SelectMany(descriptor => descriptor.PrivateMembers)
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets the descriptor of an IANA-registered key type, matched by ordinal comparison.
    /// </summary>
    /// <param name="keyType">The value of the <c>kty</c> member.</param>
    /// <param name="descriptor">The descriptor, when <paramref name="keyType"/> is a standard key type.</param>
    /// <returns><c>true</c> when the key type is a standard one; otherwise, <c>false</c>.</returns>
    public static bool TryGetStandard(
        string keyType,
        [NotNullWhen(true)] out JsonWebKeyTypeDescriptor? descriptor
    ) => StandardByName.TryGetValue(keyType, out descriptor);
}
