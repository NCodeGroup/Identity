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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Provides a base class for <see cref="IJsonWebKeyConverter"/> implementations that convert a
/// single, specific <see cref="SecretKey"/> type into a <see cref="JsonWebKey"/>.
/// </summary>
/// <typeparam name="TSecretKey">The type of <see cref="SecretKey"/> handled by this converter.</typeparam>
[PublicAPI]
public abstract class JsonWebKeyConverter<TSecretKey> : IJsonWebKeyConverter
    where TSecretKey : SecretKey
{
    /// <inheritdoc />
    public bool TryConvert(SecretKey secretKey, [NotNullWhen(true)] out JsonWebKey? jsonWebKey)
    {
        if (secretKey is TSecretKey typedSecretKey)
        {
            jsonWebKey = Convert(typedSecretKey);
            return jsonWebKey is not null;
        }

        jsonWebKey = null;
        return false;
    }

    /// <summary>
    /// Converts the strongly-typed <paramref name="secretKey"/> into a <see cref="JsonWebKey"/>, or
    /// returns <c>null</c> when the key cannot be represented (for example, an unsupported curve).
    /// </summary>
    /// <param name="secretKey">The <typeparamref name="TSecretKey"/> to convert.</param>
    /// <returns>The converted <see cref="JsonWebKey"/>, or <c>null</c>.</returns>
    protected abstract JsonWebKey? Convert(TSecretKey secretKey);

    /// <summary>
    /// Returns <c>null</c> when <paramref name="value"/> is <c>null</c> or empty; otherwise, the value.
    /// Useful for omitting optional JWK members that have no value.
    /// </summary>
    protected static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
